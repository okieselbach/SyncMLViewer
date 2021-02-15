using System.Diagnostics;
using Microsoft.Win32;

namespace SyncMLViewer
{
    internal static class Helper
    {
        public static void OpenRegistry(string path)
        {
            using (var registryKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default)
                .OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Applets\Regedit", true))
            {
                registryKey?.SetValue("LastKey", path);
            }

            var processes = Process.GetProcessesByName("regedit");
            foreach (var proc in processes)
            {
                proc.Kill();
            }

            var p = new Process
            {
                StartInfo =
                {
                    UseShellExecute = false,
                    FileName = "regedit.exe"
                }
            };
            p.Start();
            p.Dispose();
        }
    }
}
