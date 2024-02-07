using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;

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

        public static void OpenUrl(string path)
        {
            try
            {
                var exp = new Process
                {
                    StartInfo =
                    {
                        UseShellExecute = true,
                        FileName = path
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

        public static async Task RunMdmDiagnosticsTool(string scenario)
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "MDMDiagnostics", "MdmDiagnosticsTool");
            Directory.CreateDirectory(path);

            string argument = string.Empty;
            switch (scenario)
            {
                case "Autopilot":
                case "DeviceEnrollment":
                case "DeviceProvisioning":
                case "TPM":
                    argument = $"-area {scenario} -zip {Path.Combine(path, scenario + ".zip")}";
                    break;
                default:
                    argument = $"-out {path}";
                    return;
            };

            await Task.Factory.StartNew(() =>
            {
                var p = new Process
                {
                    StartInfo =
                    {
                        UseShellExecute = false,
                        FileName = "MdmDiagnosticsTool.exe",
                        Arguments = argument,
                        CreateNoWindow = true
                    }
                };

                p.Start();
                p.WaitForExit();
                Debug.WriteLine($"MdmDiagnosticsTool ExitCode: {p.ExitCode}");
            });
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

        public static string RunCommand(string command, string arguments)
        {
            var output = string.Empty;
            try
            {
                var p = new Process
                {
                    StartInfo =
                    {
                        UseShellExecute = false,
                        FileName = command,
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                p.Start();
                output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                Debug.WriteLine($"RunCommand: {command} {arguments} ExitCode: {p.ExitCode}");

                p.Dispose();
            }
            catch (Exception)
            {
                // ignore
            }

            return output;
        }

        public static string RegexExtractStringValueAfterKeyAndColon(string input, string key)
        {
            string pattern = $@"{key}\s+:\s+(.+)";
            Match match = Regex.Match(input, pattern, RegexOptions.IgnoreCase);

            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }

            return null;
        }

        public static List<XmlDocument> ParseXmlFiles(string directoryPath)
        {
            List<XmlDocument> xmlDocuments = new List<XmlDocument>();

            try
            {
                string[] xmlFiles = Directory.GetFiles(directoryPath, "*.xml");

                foreach (string xmlFile in xmlFiles)
                {
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(xmlFile);
                    xmlDocuments.Add(xmlDoc);
                }
            }
            catch (Exception)
            {
                // ignore
            }

            return xmlDocuments;
        }
    }
}
