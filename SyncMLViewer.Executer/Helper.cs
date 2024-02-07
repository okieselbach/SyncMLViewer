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

    }
}
