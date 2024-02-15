using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SyncMLViewer.Executer
{
    internal class Helper
    {
        internal static readonly uint TOKEN_QUERY = 8u;

        internal enum TOKEN_INFORMATION_CLASS
        {
            TokenUser = 1,
            TokenGroups,
            TokenPrivileges,
            TokenOwner,
            TokenPrimaryGroup,
            TokenDefaultDacl,
            TokenSource,
            TokenType,
            TokenImpersonationLevel,
            TokenStatistics,
            TokenRestrictedSids,
            TokenSessionId,
            TokenGroupsAndPrivileges,
            TokenSessionReference,
            TokenSandBoxInert,
            TokenAuditPolicy,
            TokenOrigin,
            TokenElevationType,
            TokenLinkedToken,
            TokenElevation,
            TokenHasRestrictions,
            TokenAccessInformation,
            TokenVirtualizationAllowed,
            TokenVirtualizationEnabled,
            TokenIntegrityLevel,
            TokenUIAccess,
            TokenMandatoryPolicy,
            TokenLogonSid,
            MaxTokenInfoClass
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenProcessToken([In] IntPtr ProcessHandle, [In] uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetTokenInformation(IntPtr TokenHandle, TOKEN_INFORMATION_CLASS TokenInformationClass, IntPtr TokenInformation, int TokenInformationLength, out int ReturnLength);

        [DllImport("kernel32.dll")]
        internal static extern int CloseHandle([In] IntPtr hHandle);

        internal unsafe static bool? IsElevated()
        {
            Process currentProcess = Process.GetCurrentProcess();
            IntPtr TokenHandle = IntPtr.Zero;
            if (!OpenProcessToken(currentProcess.Handle, TOKEN_QUERY, out TokenHandle))
            {
                return null;
            }

            int num = 0;
            int ReturnLength;
            bool tokenInformation = GetTokenInformation(TokenHandle, TOKEN_INFORMATION_CLASS.TokenElevation, (IntPtr)(&num), 4, out ReturnLength);
            CloseHandle(TokenHandle);
            if (!tokenInformation)
            {
                return null;
            }

            GC.KeepAlive(currentProcess);
            return num != 0;
        }

        public static void GetEnrollmentGuids(out string OmaDmAccountIdMDM, out string OmaDmAccountIdMMPC)
        {
            OmaDmAccountIdMDM = "";
            OmaDmAccountIdMMPC = "";

            using (var registryKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default)
                .OpenSubKey(@"SOFTWARE\Microsoft\Provisioning\OMADM\Accounts"))
            {
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
                                string ServerId = registryKey2.GetValue("ServerId").ToString();
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

                // Fallback: if only 1 keyname is available and we couldn't fetch the ServerId (different MDM provider for example) we use the key name as the MDM account
                if (string.IsNullOrEmpty(OmaDmAccountIdMDM) && OmaDmAccountIds.Length == 1)
                {
                    using (var regKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default).OpenSubKey(@"SOFTWARE\Microsoft\Provisioning\OMADM\Accounts"))
                    {
                        if (regKey == null) return;
                        OmaDmAccountIdMDM = registryKey.GetSubKeyNames().FirstOrDefault();
                    }
                }

            }
        }

        public static bool SetRegistryLocalMachineDWordValue(string key, string value, object data)
        {
            try
            {
                using (var regKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default).OpenSubKey(key, true))
                {
                    if (regKey == null) return false;
                    regKey.SetValue(value, data, RegistryValueKind.DWord);
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
