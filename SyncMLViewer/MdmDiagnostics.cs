using System;
using System.Linq;
using Microsoft.Win32;

namespace SyncMLViewer
{
    internal class MdmDiagnostics
    {
        private static string RegKeyWindowsCurrentVersion =>
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion";

        public string OmaDmAccountId { get; }
        public string AadTenantId { get; }
        public string Upn { get; }

        public static string Hostname => Environment.MachineName;
        public static string Bits => Environment.Is64BitOperatingSystem ? "64" : "32";
        public static string Version => Environment.OSVersion.Version.ToString();
        public static string OsVersion => (string)Registry.GetValue(RegKeyWindowsCurrentVersion, "ProductName", string.Empty);
        public static string Edition => (string)Registry.GetValue(RegKeyWindowsCurrentVersion, "EditionID", string.Empty);
        public static string CompositionEdition => (string)Registry.GetValue(RegKeyWindowsCurrentVersion, "CompositionEditionID", string.Empty);
        public static string CurrentBuild => (string)Registry.GetValue(RegKeyWindowsCurrentVersion, "CurrentBuild", string.Empty);
        public static string ReleaseId => (string)Registry.GetValue(RegKeyWindowsCurrentVersion, "ReleaseID", string.Empty);
        public static string BuildBranch => (string)Registry.GetValue(RegKeyWindowsCurrentVersion, "BuildBranch", string.Empty);
        public static string BuildRevision => (string)Registry.GetValue(RegKeyWindowsCurrentVersion, "UBR", string.Empty).ToString();

        public MdmDiagnostics()
        {
            OmaDmAccountId = "";
            AadTenantId = "";
            Upn = "";

            using (var registryKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default)
                .OpenSubKey(@"SOFTWARE\Microsoft\Provisioning\OMADM\Accounts"))
            {
                if (registryKey == null) return;
                OmaDmAccountId = registryKey.GetSubKeyNames().FirstOrDefault();
            }

            try
            {
                using (var registryKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default)
                    .OpenSubKey($"SOFTWARE\\Microsoft\\Enrollments\\{OmaDmAccountId}"))
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
    }
}