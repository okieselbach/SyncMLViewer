using System;
using System.Linq;
using Microsoft.Win32;

namespace SyncMLViewer
{
    internal class MdmDiagnostics
    {
        // TODO: 

        public string OmaDmAccountId { get; }
        public string Hostname { get; }
        public string AadTenantId { get; }
        public string Upn { get; }

        public MdmDiagnostics()
        {
            OmaDmAccountId = "";
            Hostname = "";
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
                    AadTenantId = registryKey.GetValue("AADTenantID").ToString();
                    Upn = registryKey.GetValue("UPN").ToString();
                }
            }
            catch (Exception)
            {
                // Ignored
            }

            Hostname = Environment.MachineName;
        }
    }
}