using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Win32;
using FileVersionInfo = System.Diagnostics.FileVersionInfo;

namespace SyncMLViewer
{
    internal class MdmDiagnostics
    {
        private static string RegKeyWindowsCurrentVersion =>
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion";

        public string OmaDmAccountIdMDM { get; }
        public string OmaDmAccountIdMMPC { get; }
        public string AadTenantId { get; }
        public string EnrollmentUpn { get; }
        public string ServerId { get; }

        // Diagnostic properties
        public string EnrollmentState { get; }
        public string ManagementAuthority { get; }
        public string EntraDeviceId { get; }
        public string IntuneDeviceId { get; }
        public string SerialNumber { get; }
        public string AutopilotRegistered { get; }
        public string AutopilotProfileName { get; }
        public string AutopilotDeploymentProfile { get; }
        public string DeclaredConfigurationEnabled { get; }

        public static string LogonUsername = System.Security.Principal.WindowsIdentity.GetCurrent().Name;

        public static string LogonUserSid = System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;

        public static string Hostname => Environment.MachineName;
        public static string Bits => Environment.Is64BitOperatingSystem ? "64" : "32";
        public static string Version => Environment.OSVersion.Version.ToString();
        public static string OsVersion => GetOsName();
        public static string Edition => (string)Registry.GetValue(RegKeyWindowsCurrentVersion, "EditionID", string.Empty);
        public static string CompositionEdition => (string)Registry.GetValue(RegKeyWindowsCurrentVersion, "CompositionEditionID", string.Empty);
        public static string CurrentBuild => (string)Registry.GetValue(RegKeyWindowsCurrentVersion, "CurrentBuild", string.Empty);
        public static string BuildBranch => (string)Registry.GetValue(RegKeyWindowsCurrentVersion, "BuildBranch", string.Empty);
        public static string DisplayVersion => (string)Registry.GetValue(RegKeyWindowsCurrentVersion, "DisplayVersion", string.Empty);
        public static string BuildRevision => (string)Registry.GetValue(RegKeyWindowsCurrentVersion, "UBR", string.Empty).ToString();
        public static string IntuneAgentVersion => GetIntuneFileVersionInfo()?.FileVersion;

        public MdmDiagnostics()
        {
            OmaDmAccountIdMDM = "";
            OmaDmAccountIdMMPC = "";
            AadTenantId = "";
            EnrollmentUpn = "";
            ServerId = "";

            // Initialize properties
            EnrollmentState = "";
            ManagementAuthority = "";
            EntraDeviceId = "";
            IntuneDeviceId = "";
            SerialNumber = "";
            AutopilotRegistered = "";
            AutopilotProfileName = "";
            AutopilotDeploymentProfile = "";
            DeclaredConfigurationEnabled = "";

            // Get Serial Number from BIOS (always available)
            SerialNumber = GetSerialNumber();

            // Get Entra Device ID from certificate (MS-Organization-Access)
            EntraDeviceId = GetEntraDeviceIdFromCertificate();

            // Get Intune Device ID from certificate (Microsoft Intune MDM Device CA)
            IntuneDeviceId = GetIntuneDeviceIdFromCertificate();

            // Get Tenant ID (always available if AAD joined)
            AadTenantId = GetAadTenantId();

            // Get Autopilot information
            GetAutopilotInfo(out string registered, out string profileName, out string deploymentProfile);
            AutopilotRegistered = registered;
            AutopilotProfileName = profileName;
            AutopilotDeploymentProfile = deploymentProfile;

            // Get Declared Configuration status
            DeclaredConfigurationEnabled = GetDeclaredConfigurationStatus();

            using (var registryKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default)
                .OpenSubKey(@"SOFTWARE\Microsoft\Provisioning\OMADM\Accounts"))
            {
                if (registryKey == null)
                {
                    EnrollmentState = "Not Enrolled";
                    return;
                }

                var OmaDmAccountIds = registryKey.GetSubKeyNames();
                
                foreach (var OmaDmAccountId in OmaDmAccountIds)
                {
                    using (var registryKey2 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default)
                                                .OpenSubKey($"SOFTWARE\\Microsoft\\Provisioning\\OMADM\\Accounts\\{OmaDmAccountId}\\Protected"))
                    {
                        if (registryKey2 == null) continue;
                        var OmaDmAccountNames = registryKey2.GetValueNames();
                        foreach (var OmaDmAccountName in OmaDmAccountNames)
                        {
                            if (OmaDmAccountName == "ServerId")
                            {
                                ServerId = registryKey2.GetValue("ServerId").ToString();

                                if (ServerId == "MS DM Server")
                                {
                                    OmaDmAccountIdMDM = OmaDmAccountId;
                                }
                                if (ServerId == "Microsoft Device Management")
                                {
                                    OmaDmAccountIdMMPC = OmaDmAccountId;
                                }
                            }
                        }
                    }
                }

                // Fallback: if only 1 keyname is available and we couldn't fetch the ServerId
                if (string.IsNullOrEmpty(OmaDmAccountIdMDM) && OmaDmAccountIds.Length == 1)
                {
                    OmaDmAccountIdMDM = OmaDmAccountIds.FirstOrDefault();
                }
            }

            // Get enrollment details from Enrollments registry
            try
            {
                using (var registryKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default)
                    .OpenSubKey($"SOFTWARE\\Microsoft\\Enrollments\\{OmaDmAccountIdMDM}"))
                {
                    if (registryKey != null)
                    {
                        EnrollmentUpn = registryKey.GetValue("UPN")?.ToString() ?? "";
                        
                        // Get EnrollmentType to determine Management Authority
                        var enrollmentType = registryKey.GetValue("EnrollmentType");
                        if (enrollmentType != null)
                        {
                            ManagementAuthority = GetManagementAuthorityFromEnrollmentType((int)enrollmentType);
                            EnrollmentState = "Enrolled";
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Ignored
            }
        }

        /// <summary>
        /// Gets the Entra (Azure AD) Device ID from the MS-Organization-Access certificate.
        /// The certificate's Subject (CN=) contains the device ID.
        /// </summary>
        private static string GetEntraDeviceIdFromCertificate()
        {
            try
            {
                using (var store = new X509Store(StoreName.My, StoreLocation.LocalMachine))
                {
                    store.Open(OpenFlags.ReadOnly);

                    foreach (var cert in store.Certificates)
                    {
                        // MS-Organization-Access certificate is issued by MS-Organization-Access
                        if (cert.Issuer.Contains("MS-Organization-Access"))
                        {
                            // Subject is in format: CN={DeviceId}
                            var subject = cert.Subject;
                            if (subject.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
                            {
                                var deviceId = subject.Substring(3).Trim();
                                Debug.WriteLine($"Found MS-Organization-Access cert: DeviceId={deviceId}");
                                return deviceId;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetEntraDeviceIdFromCertificate error: {ex.Message}");
            }
            return "";
        }

        /// <summary>
        /// Gets the Intune Device ID from the Microsoft Intune MDM Device CA certificate.
        /// The certificate's Subject (CN=) contains the Intune device ID.
        /// </summary>
        private static string GetIntuneDeviceIdFromCertificate()
        {
            try
            {
                using (var store = new X509Store(StoreName.My, StoreLocation.LocalMachine))
                {
                    store.Open(OpenFlags.ReadOnly);

                    foreach (var cert in store.Certificates)
                    {
                        // Microsoft Intune MDM Device CA certificate
                        if (cert.Issuer.Contains("Microsoft Intune MDM Device CA"))
                        {
                            // Subject is in format: CN={IntuneDeviceId}
                            var subject = cert.Subject;
                            if (subject.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
                            {
                                var deviceId = subject.Substring(3).Trim();
                                Debug.WriteLine($"Found Intune MDM cert: DeviceId={deviceId}");
                                return deviceId;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetIntuneDeviceIdFromCertificate error: {ex.Message}");
            }
            return "";
        }

        private static string GetAadTenantId()
        {
            try
            {
                using (var registryKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default)
                    .OpenSubKey(@"SYSTEM\CurrentControlSet\Control\CloudDomainJoin\TenantInfo"))
                {
                    if (registryKey != null)
                    {
                        return registryKey.GetSubKeyNames().FirstOrDefault() ?? "";
                    }
                }
            }
            catch (Exception)
            {
                // Ignored
            }
            return "";
        }

        private static string GetManagementAuthorityFromEnrollmentType(int enrollmentType)
        {
            switch (enrollmentType)
            {
                case 6:
                    return "Intune (MDM)";
                case 13:
                    return "Co-managed";
                case 14:
                case 20:
                    return "Local MDM";
                case 1:
                    return "Azure AD Join";
                case 8:
                    return "Device Enrollment";
                default:
                    return $"Unknown ({enrollmentType})";
            }
        }

        private static string GetSerialNumber()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        return obj["SerialNumber"]?.ToString() ?? "";
                    }
                }
            }
            catch (Exception)
            {
                // Ignored
            }
            return "";
        }

        private static void GetAutopilotInfo(out string registered, out string profileName, out string deploymentProfile)
        {
            registered = "No";
            profileName = "";
            deploymentProfile = "";

            try
            {
                // Check Autopilot registration in AutopilotSettings
                using (var registryKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default)
                    .OpenSubKey(@"SOFTWARE\Microsoft\Provisioning\AutopilotSettings"))
                {
                    if (registryKey != null)
                    {
                        registered = "Yes";
                        
                        // Get profile/tenant domain
                        var cloudAssignedProfileValue = registryKey.GetValue("CloudAssignedTenantDomain");
                        if (cloudAssignedProfileValue != null)
                        {
                            profileName = cloudAssignedProfileValue.ToString();
                        }

                        // Get deployment mode from CloudAssignedOobeConfig
                        var oobeConfig = registryKey.GetValue("CloudAssignedOobeConfig");
                        if (oobeConfig != null && int.TryParse(oobeConfig.ToString(), out int oobeConfigValue))
                        {
                            deploymentProfile = GetDeploymentProfileType(oobeConfigValue);
                        }

                        // If still no deployment profile, try other indicators
                        if (string.IsNullOrEmpty(deploymentProfile))
                        {
                            var deviceName = registryKey.GetValue("CloudAssignedDeviceName");
                            
                            if (deviceName != null && !string.IsNullOrEmpty(deviceName.ToString()))
                            {
                                deploymentProfile = "Pre-Provisioned / Self-Deploying";
                            }
                            else
                            {
                                deploymentProfile = "User-Driven";
                            }
                        }
                    }
                }

                // Alternative: Check Diagnostics\Autopilot for more info
                if (string.IsNullOrEmpty(profileName) || registered == "No")
                {
                    using (var registryKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default)
                        .OpenSubKey(@"SOFTWARE\Microsoft\Provisioning\Diagnostics\Autopilot"))
                    {
                        if (registryKey != null)
                        {
                            registered = "Yes";
                            
                            var tenantDomain = registryKey.GetValue("CloudAssignedTenantDomain");
                            if (tenantDomain != null && string.IsNullOrEmpty(profileName))
                            {
                                profileName = tenantDomain.ToString();
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Ignored
            }
        }

        private static string GetDeploymentProfileType(int oobeConfig)
        {
            // CloudAssignedOobeConfig is a bitmask
            // Check for self-deploying mode (bit 10 = 1024)
            if ((oobeConfig & 1024) != 0)
            {
                return "Self-Deploying";
            }
            
            // Check for pre-provisioned/white glove (bit 11 = 2048)
            if ((oobeConfig & 2048) != 0)
            {
                return "Pre-Provisioned (White Glove)";
            }
            
            // If any OOBE customization is set, it's likely user-driven
            if (oobeConfig > 0)
            {
                return "User-Driven";
            }
            
            return "Standard";
        }

        private static string GetDeclaredConfigurationStatus()
        {
            try
            {
                using (var registryKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default)
                    .OpenSubKey(@"SOFTWARE\Microsoft\DeclaredConfiguration"))
                {
                    if (registryKey != null)
                    {
                        var dcPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\DC\HostOS");
                        if (Directory.Exists(dcPath))
                        {
                            var files = Directory.GetFiles(dcPath, "*.xml", SearchOption.AllDirectories);
                            if (files.Length > 0)
                            {
                                return $"Yes ({files.Length} document(s))";
                            }
                        }
                        return "Yes (No documents)";
                    }
                }
            }
            catch (Exception)
            {
                // Ignored
            }
            return "No";
        }

        public static string GetOsName()
        {
            var osName = string.Empty;

            try
            {
                var wmi = new ManagementObjectSearcher("select * from Win32_OperatingSystem");
                foreach (ManagementObject obj in wmi.Get())
                {
                    osName = obj["Caption"] as string;
                    break;
                }
            }
            catch (Exception)
            {
                // prevent exceptions...
            }

            return osName;
        }

        public static FileVersionInfo GetIntuneFileVersionInfo()
        {
            try
            {
                var fileVersionInfo = FileVersionInfo.GetVersionInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + @"\Microsoft Intune Management Extension\Microsoft.Management.Services.IntuneWindowsAgent.exe"));
                return fileVersionInfo;
            }
            catch (Exception)
            {
                // prevent exceptions...
            }

            return null;
        }
    }
}