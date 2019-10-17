using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace SyncMLViewer
{
    internal class MdmDiagnostics
    {
        // TODO: 

        public string OmaDmAccountId { get; set; }
        public string Hostname { get; set; }
        public string AadTenantId { get; set; }
        public string Upn { get; set; }

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
                OmaDmAccountId = registryKey.GetSubKeyNames().FirstOrDefault<string>();
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