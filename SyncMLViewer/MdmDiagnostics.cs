using System;
using System.Linq;
using System.Management;
using System.Windows.Forms;
using System.Xml.Linq;
using Microsoft.Win32;

namespace SyncMLViewer
{
    internal class MdmDiagnostics
    {
        private static string RegKeyWindowsCurrentVersion =>
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion";

        public string OmaDmAccountIdMDM { get; }
        public string OmaDmAccountIdMMPC { get; }
        public string AadTenantId { get; }
        public string Upn { get; }
        public string ServerId { get; }

        public static string Hostname => Environment.MachineName;
        public static string Bits => Environment.Is64BitOperatingSystem ? "64" : "32";
        public static string Version => Environment.OSVersion.Version.ToString();
        public static string OsVersion => GetOsName();//(string)Registry.GetValue(RegKeyWindowsCurrentVersion, "ProductName", string.Empty);
        public static string Edition => (string)Registry.GetValue(RegKeyWindowsCurrentVersion, "EditionID", string.Empty);
        public static string CompositionEdition => (string)Registry.GetValue(RegKeyWindowsCurrentVersion, "CompositionEditionID", string.Empty);
        public static string CurrentBuild => (string)Registry.GetValue(RegKeyWindowsCurrentVersion, "CurrentBuild", string.Empty);
        //public static string ReleaseId => (string)Registry.GetValue(RegKeyWindowsCurrentVersion, "ReleaseID", string.Empty);
        public static string BuildBranch => (string)Registry.GetValue(RegKeyWindowsCurrentVersion, "BuildBranch", string.Empty);
        public static string DisplayVersion => (string)Registry.GetValue(RegKeyWindowsCurrentVersion, "DisplayVersion", string.Empty);
        public static string BuildRevision => (string)Registry.GetValue(RegKeyWindowsCurrentVersion, "UBR", string.Empty).ToString();

        public MdmDiagnostics()
        {
            OmaDmAccountIdMDM = "";
            OmaDmAccountIdMMPC = "";
            AadTenantId = "";
            Upn = "";
            ServerId = "";

            using (var registryKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default)
                .OpenSubKey(@"SOFTWARE\Microsoft\Provisioning\OMADM\Accounts"))
            {
                //if (registryKey == null) return;
                //OmaDmAccountId = registryKey.GetSubKeyNames().FirstOrDefault();

                var OmaDmAccountIds = registryKey.GetSubKeyNames(); // should return 1 value if MDM enrolled and 2 values if dual enrolled (linkedEnrollemnt)
                foreach (var OmaDmAccountId in OmaDmAccountIds)
                {
                    using (var registryKey2 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default)
                                               .OpenSubKey($"SOFTWARE\\Microsoft\\Provisioning\\OMADM\\Accounts\\{OmaDmAccountId}\\Protected"))
                    {
                        if (registryKey2 == null) return;
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
            }

            try
            {
                using (var registryKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default)
                    .OpenSubKey($"SOFTWARE\\Microsoft\\Enrollments\\{OmaDmAccountIdMDM}"))
                {
                    if (registryKey == null) return;
                    // seems not be available all the time using TenantInfo now...
                    //AadTenantId = registryKey.GetValue("AADTenantID").ToString();
                    Upn = registryKey.GetValue("UPN").ToString();
                }
            }
            catch (Exception)
            {
                // Ignored
            }

            using (var registryKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default)
                .OpenSubKey(@"SYSTEM\CurrentControlSet\Control\CloudDomainJoin\TenantInfo"))
            {
                if (registryKey == null) return;
                AadTenantId = registryKey.GetSubKeyNames().FirstOrDefault();
            }
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
    }
}