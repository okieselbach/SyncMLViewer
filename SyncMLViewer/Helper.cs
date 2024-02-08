using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
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

        public static void OpenEventLog(string logName)
        {
            var path = Environment.ExpandEnvironmentVariables(@"%SystemRoot%\system32");
            try
            {
                var exp = new Process
                {
                    StartInfo =
                    {
                        FileName = $"{path}\\mmc.exe",
                        Arguments = $"\"{path}\\eventvwr.msc\" /c:\"{logName}\""
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
                MessageBox.Show($"Folder '{path}' does not exist.", "Open folder", MessageBoxButton.OK, MessageBoxImage.Information);
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

        [DllImport("MdmDiagnostics.dll")]
        public static extern Int64 CreateMdmEnterpriseDiagnosticHTMLReport([MarshalAs(UnmanagedType.LPWStr)] string path);

        public static async Task CreateAdvancedDiagnosticsReport()
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "MDMDiagnostics", "MDMDiagReport.html");

            await Task.Factory.StartNew(() =>
            {
                Int64 result = CreateMdmEnterpriseDiagnosticHTMLReport(path);
                Debug.WriteLine($"MdmDiagnosticsTool ExitCode: {result}");
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

        public static string TryFormatTruncatedXml(string xml)
        {
            // sometimes data gets truncated, so we try to format it with a best effort logic
            try
            {
                // Define patterns for opening, closing, and self-closing tags
                string openTagPattern = @"<[^/][^>]*>";
                string closeTagPattern = @"</[^>]+>";
                string selfClosingTagPattern = @"<[^>]+/>";
                string valuePattern = @"[^<]+";

                string indent = "    ";

                // Split XML string by opening, closing, and self-closing tags
                string[] rawTokens = Regex.Split(xml, $@"({openTagPattern}|{closeTagPattern}|{selfClosingTagPattern})");
                string[] tokens = rawTokens.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

                if (Regex.IsMatch(tokens[0], $@"^{closeTagPattern}"))
                {
                    tokens[0] = string.Empty;
                }

                int level = 0;
                var formattedXml = new StringBuilder();

                for (int index = 0; index < tokens.Length; index++)
                {
                    string token = tokens[index];

                    // if a section start is found <!-- 2/1/2024 8:36:04 AM --> reset indent level to 0 
                    string patternXmlCommentWithDateTime = @"<!--\s*\d{1,2}/\d{1,2}/\d{4}\s+\d{1,2}:\d{2}(:\d{2})?\s*([AP]M)?\s*-->";
                    if (Regex.IsMatch(token, $@"^{patternXmlCommentWithDateTime}"))
                    {
                        level = 0;
                    }

                    if (Regex.IsMatch(token, $@"^{openTagPattern}"))
                    {
                        formattedXml.Append(token);

                        if (index + 2 < tokens.Length)
                        {
                            if (Regex.IsMatch(token, $@"^{openTagPattern}") && Regex.IsMatch(tokens[index + 1], $@"^{valuePattern}") && Regex.IsMatch(tokens[index + 2], $@"^{closeTagPattern}"))
                            {
                                formattedXml.Append(tokens[index + 1]);
                                formattedXml.Append(tokens[index + 2]);
                                index = index + 2;
                                level--;
                            }
                            level++;
                            formattedXml.AppendLine();
                            for (int i = 0; i < level; i++)
                            {
                                formattedXml.Append(indent);
                            }
                        }
                    }
                    else if (Regex.IsMatch(token, $@"^{closeTagPattern}"))
                    {
                        formattedXml.Append(token);

                        level--;
                        formattedXml.AppendLine();
                        for (int i = 0; i < level; i++)
                        {
                            formattedXml.Append(indent);
                        }
                    }
                    else
                    {
                        formattedXml.Append(token);
                    }
                }

                return formattedXml.ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error formatting XML: {ex.Message}");
                return xml;
            }
        }

        [DllImport("kernel32.dll")]
        public static extern uint FormatMessage(uint dwFlags, IntPtr lpSource, uint dwMessageId, uint dwLanguageId, StringBuilder lpBuffer, uint nSize, IntPtr Arguments);

        public static string GetErrorMessage(uint errorCode)
        {
            const uint FORMAT_MESSAGE_FROM_SYSTEM = 0x00001000;
            const uint FORMAT_MESSAGE_IGNORE_INSERTS = 0x00000200;
            const uint LANG_USER_DEFAULT = 0x0400;

            StringBuilder messageBuffer = new StringBuilder(256);

            uint result = FormatMessage(
                FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
                IntPtr.Zero,
                (uint)errorCode,
                LANG_USER_DEFAULT,
                messageBuffer,
                (uint)messageBuffer.Capacity,
                IntPtr.Zero);

            if (result != 0)
            {
                return messageBuffer.ToString();
            }
            else
            {
                return $"Error code {errorCode}";
            }
        }

        public static string DecodePEMCertificate(string pemEncodedCertificate)
        {
            // Remove PEM header and footer
            string base64EncodedCertificate = pemEncodedCertificate
                .Replace("-----BEGIN CERTIFICATE-----", "")
                .Replace("-----END CERTIFICATE-----", "")
                .Replace("\n", "")
                .Replace("\r", "")
                .Replace(" ", "");

            X509Certificate2 certificate;
            try
            {
                byte[] certBytes = Convert.FromBase64String(base64EncodedCertificate);
                certificate = new X509Certificate2(certBytes);
            }
            catch (Exception)
            {
                return null;
            }

            var sb = new StringBuilder();

            sb.AppendLine("Subject: " + certificate.Subject);
            sb.AppendLine("Friendly Name: " + certificate.FriendlyName);
            sb.AppendLine("Issuer: " + certificate.Issuer);
            sb.AppendLine("Issuer Name: " + certificate.IssuerName.Name);
            sb.AppendLine("Thumbprint: " + certificate.Thumbprint);
            sb.AppendLine("Serial Number: " + certificate.SerialNumber);
            sb.AppendLine("Not Before: " + certificate.NotBefore);
            sb.AppendLine("Not After: " + certificate.NotAfter);
            //sb.AppendLine("Public Key Algorithm: " + certificate.GetKeyAlgorithm());
            sb.AppendLine("Signature Algorithm: " + certificate.SignatureAlgorithm.FriendlyName);
            sb.AppendLine("Version: " + certificate.Version);
            sb.AppendLine("Has Private Key: " + certificate.HasPrivateKey);
            foreach (X509Extension extension in certificate.Extensions)
            {
                // Print extensions
                sb.AppendLine("Extension: " + extension.Oid.FriendlyName + " - " + extension.Format(true));
            }

            return sb.ToString();
        }
    }
}
