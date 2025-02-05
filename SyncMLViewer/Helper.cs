﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
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
                var p = new Process
                {
                    StartInfo =
                    {
                        FileName = $"{path}\\mmc.exe",
                        Arguments = $"\"{path}\\eventvwr.msc\" /c:\"{logName}\""
                    }
                };
                p.Start();
                p.Dispose();
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
                    var p = new Process
                    {
                        StartInfo = 
                        {
                            FileName = "explorer.exe",
                            Arguments = path
                        }
                    };
                    p.Start();
                    p.Dispose();
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
                var p = new Process
                {
                    StartInfo =
                    {
                        UseShellExecute = true,
                        FileName = path
                    }
                };
                p.Start();
                p.Dispose();
            }
            catch (Exception)
            {
                // prevent exceptions if folder does not exist
            }
        }

        public static void SearchWithGoogle(string searchText)
        {
            try
            {
                var p = new Process
                {
                    StartInfo =
                    {
                        UseShellExecute = true,
                        FileName = "https://www.google.com/search?q=" + HttpUtility.UrlEncode(searchText)
            }
                };
                p.Start();
                p.Dispose();
            }
            catch (Exception)
            {
                MessageBox.Show("Error opening browser.", "Search with Google", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static void OpenInNotepad(string text)
        {
            try
            {
                var tempFilePath = Path.GetTempFileName();
                File.WriteAllText(tempFilePath, text);

                var p = new Process
                {
                    StartInfo =
                    {
                        UseShellExecute = true,
                        FileName = "Notepad.exe",
                        Arguments = tempFilePath
                    },
                    EnableRaisingEvents = true
                };

                p.Exited += (sender, e) =>
                {
                    try
                    {
                        File.Delete(tempFilePath);
                    }
                    catch (Exception)
                    {
                        // ignore
                    }
                };

                if (p.Start())
                {
                    p.WaitForExit();
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Error opening Notepad.", "Open in Notepad", MessageBoxButton.OK, MessageBoxImage.Error);
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
            var p = new Process
            {
                StartInfo =
                {
                    FileName = "explorer.exe",
                    Arguments = path
                }
            };
            p.Start();
            p.Dispose();
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
                    break;
            };

            await Task.Factory.StartNew(() =>
            {
                var tool = new Process
                {
                    StartInfo =
                    {
                        UseShellExecute = false,
                        FileName = "MdmDiagnosticsTool.exe",
                        Arguments = argument,
                        CreateNoWindow = true
                    }
                };

                tool.Start();
                tool.WaitForExit();
                Debug.WriteLine($"MdmDiagnosticsTool ExitCode: {tool.ExitCode}");
            });
            var p = new Process
            {
                StartInfo =
                {
                    FileName = "explorer.exe",
                    Arguments = path
                }
            };
            p.Start();
            p.Dispose();
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

        public static string ConvertTextToHex(string input)
        {
            //StringBuilder hexBuilder = new StringBuilder();
            //int count = 0;

            //foreach (char c in input)
            //{
            //    hexBuilder.AppendFormat("{0:X2} ", (int)c);
            //    count++;

            //    // after 16 Bytes (32 Hex-chars) add line break
            //    if (count % 16 == 0)
            //    {
            //        // remove last char before line break
            //        if (hexBuilder.Length > 0 && hexBuilder[hexBuilder.Length - 1] == ' ')
            //        {
            //            hexBuilder.Length -= 1;
            //        }
            //        hexBuilder.AppendLine();
            //    }
            //}

            //return hexBuilder.ToString().TrimEnd();

            StringBuilder hexBuilder = new StringBuilder();
            StringBuilder asciiBuilder = new StringBuilder();
            int count = 0;

            foreach (char c in input)
            {
                // Convert the character to its two-digit hex representation, followed by a space.
                hexBuilder.AppendFormat("{0:X2} ", (int)c);

                // For the ASCII representation: use the character if it's printable (from 0x20 to 0x7E), otherwise use '.'
                if (c >= 0x20 && c <= 0x7E)
                {
                    asciiBuilder.Append(c);
                }
                else
                {
                    asciiBuilder.Append('.');
                }

                count++;

                // Every 16 bytes, start a new line
                if (count % 16 == 0)
                {
                    // Remove the trailing space
                    if (hexBuilder.Length > 0 && hexBuilder[hexBuilder.Length - 1] == ' ')
                    {
                        hexBuilder.Length -= 1;
                    }

                    // Add two spaces before the ASCII part
                    hexBuilder.Append("  ");
                    hexBuilder.Append(asciiBuilder.ToString());
                    hexBuilder.AppendLine();

                    // Reset the ASCII builder for the next line
                    asciiBuilder.Clear();
                }
            }

            // If the last line wasn't complete (less than 16 bytes):
            int remainder = count % 16;
            if (remainder != 0)
            {
                // Calculate how many bytes are missing to complete the line
                int missing = 16 - remainder;

                // Each byte in hex takes "XX " -> 3 characters, so fill in the missing hex bytes with spaces
                for (int i = 0; i < missing; i++)
                {
                    hexBuilder.Append("   ");
                }

                // Add one space before the ASCII part
                hexBuilder.Append(" ");
                hexBuilder.Append(asciiBuilder.ToString());
                hexBuilder.AppendLine();
            }

            return hexBuilder.ToString();
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

                // Define the indent string with 2 whitespaces like the other xml parsing is done
                string indent = "  ";

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
                                index += 2;
                                level--;
                            }
                            formattedXml.AppendLine();

                            level++;
                            for (int i = 0; i < level; i++)
                            {
                                formattedXml.Append(indent);
                            }
                        }
                    }
                    else if (Regex.IsMatch(token, $@"^{closeTagPattern}"))
                    {
                        formattedXml.Append(token);
                        formattedXml.AppendLine();

                        level--;
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

            sb.AppendLine($"Subject: {certificate.Subject}");
            sb.AppendLine($"Friendly Name: {certificate.FriendlyName}");
            sb.AppendLine($"Issuer: {certificate.Issuer}");
            sb.AppendLine($"Issuer Name: {certificate.IssuerName.Name}");
            sb.AppendLine($"Thumbprint: {certificate.Thumbprint}");
            sb.AppendLine($"Serial Number: {certificate.SerialNumber}");
            sb.AppendLine($"Not Before: {certificate.NotBefore}");
            sb.AppendLine($"Not After: {certificate.NotAfter}");
            //sb.AppendLine($"Public Key Algorithm: {certificate.GetKeyAlgorithm()}");
            sb.AppendLine($"Signature Algorithm: {certificate.SignatureAlgorithm.FriendlyName}");
            sb.AppendLine($"Version: {certificate.Version}");
            sb.AppendLine($"Has Private Key: {certificate.HasPrivateKey}");

            foreach (X509Extension extension in certificate.Extensions)
            {
                sb.AppendLine($"Extension: {extension.Oid.FriendlyName} - {extension.Format(true)}");
            }

            return sb.ToString();
        }

        public static string GetLocalMDMEnrollment()
        {
            string enrollmentPath = @"SOFTWARE\Microsoft\Enrollments\";
            string enrollmentId = string.Empty;
            int counter = 0;

            try
            {
                using (var baseKey = Registry.LocalMachine.OpenSubKey(enrollmentPath, true))
                {
                    if (baseKey != null)
                    {
                        string[] subKeyNames = baseKey.GetSubKeyNames();

                        foreach (var subKeyName in subKeyNames)
                        {
                            try
                            {
                                using (var subKey = baseKey.OpenSubKey(subKeyName, true))
                                {
                                    if (subKey != null)
                                    {
                                        var providerId = subKey.GetValue("ProviderId") as string;
                                        var enrollmentType = subKey.GetValue("EnrollmentType") as int?;

                                        if (providerId == "Local_Management" && enrollmentType == 20)
                                        {
                                            Debug.WriteLine($"GetLocalMDMEnrollment(), Found LocalMDM Enrollment: {subKeyName}");
                                            enrollmentId = subKeyName;
                                            counter++;
                                        }
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                // ignore
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetLocalMDMEnrollment(), failed: {ex}");
            }

            if (counter == 1)
            {
                return enrollmentId;
            }
            else
            {
                if (counter == 0)
                {
                    Debug.WriteLine($"GetLocalMDMEnrollment(), Found no LocalMDM Enrollment!");
                }
                else if (counter > 1)
                {
                    Debug.WriteLine($"GetLocalMDMEnrollment(), Found multiple LocalMDM Enrollments!");
                }
                return string.Empty;
            }
        }

        public static int ClenaupEnrollments(string excludeEnrollment = "")
        {
            int counter = 0;
            string enrollmentPath = @"SOFTWARE\Microsoft\Enrollments\";

            try
            {
                using (var baseKey = Registry.LocalMachine.OpenSubKey(enrollmentPath, true))
                {
                    if (baseKey != null)
                    {
                        string[] subKeyNames = baseKey.GetSubKeyNames();

                        foreach (var subKeyName in subKeyNames)
                        {
                            if (subKeyName != excludeEnrollment)
                            {
                                try
                                {
                                    using (var subKey = baseKey.OpenSubKey(subKeyName, true))
                                    {
                                        if (subKey != null)
                                        {
                                            var providerId = subKey.GetValue("ProviderId") as string;
                                            var enrollmentType = subKey.GetValue("EnrollmentType") as int?;

                                            if (providerId == "Local_Management" && enrollmentType == 20)
                                            {
                                                Debug.WriteLine($"Found lingering LocalMDM Enrollment: {subKeyName}");

                                                baseKey.DeleteSubKey(subKeyName);
                                                Debug.WriteLine($"Deleted key: {baseKey}\\{subKeyName}");
                                                counter++;

                                                string[] basePaths = { @"SOFTWARE\Microsoft\Enrollments\Status\", @"SOFTWARE\Microsoft\Enrollments\Context\" };
                                                foreach (var basePath in basePaths)
                                                {
                                                    try
                                                    {
                                                        using (var subKey2 = Registry.LocalMachine.OpenSubKey(basePath, true))
                                                        {
                                                            if (subKey2 != null)
                                                            {
                                                                string[] subKeyNames2 = subKey2.GetSubKeyNames();

                                                                foreach (var subKeyName2 in subKeyNames2)
                                                                {
                                                                    if (subKeyName2 == subKeyName)
                                                                    {
                                                                        subKey2.DeleteSubKey(subKeyName2);
                                                                        Debug.WriteLine($"Deleted key: {subKey2}\\{subKeyName2}");
                                                                        counter++;
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                    catch (Exception)
                                                    {
                                                        // ignore
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                catch (Exception)
                                {
                                    // ignore
                                }
                            }
                            else
                            {
                                Debug.WriteLine($"Skipped Enrollment: {subKeyName}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Cleanup Enrollments failed: {ex}");
            }

            return counter;
        }
    }
}
