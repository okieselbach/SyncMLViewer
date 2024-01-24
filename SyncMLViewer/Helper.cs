using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
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

        public static void OpenFolder(string path)
        {
            if (Directory.Exists(path))
            {
                try
                {
                    var exp = new Process
                    {
                        StartInfo = 
                        {
                            FileName = "explorer.exe",
                            Arguments = path
                        }
                    };
                    exp.Start();
                    exp.Dispose();
                }
                catch (Exception)
                {
                    // prevent exceptions if folder does not exist
                }
            }
            else
            {
                MessageBox.Show("Folder does not exist.", "Open folder", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
