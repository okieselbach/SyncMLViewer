using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml;

namespace SyncMLViewer
{
    public partial class MainWindow
    {
        // Wired LAN / 802.1x profiles
        public List<WiredLanProfile> WiredLanProfileList { get; set; }

        private void ListBoxWiredLan_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (ListBoxWiredLan.SelectedItem is WiredLanProfile wiredProfile)
                {
                    TextEditorWiredLanProfiles.Text = TryFormatXml(wiredProfile.Xml);
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private void LabelBackToTopWiredLan_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                TextEditorWiredLanProfiles.ScrollToHome();
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private async void ButtonRefreshWiredLan_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await RefreshWiredLanProfilesAsync();
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private async void ButtonDeleteWiredLan_Click(object sender, RoutedEventArgs e)
        {
            if (!(ListBoxWiredLan.SelectedItem is WiredLanProfile wiredProfile))
            {
                return;
            }

            string warningMessage;
            string interfaceToDelete;

            if (wiredProfile.Location == WiredLanProfileLocation.Machine)
            {
                // Machine profiles - need to specify an interface to delete from
                var interfaces = GetEthernetInterfaceNames();
                
                if (interfaces.Count == 0)
                {
                    MessageBox.Show(
                        "No Ethernet interfaces found to delete the profile from.",
                        "SyncML Viewer",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (interfaces.Count == 1)
                {
                    interfaceToDelete = interfaces[0];
                    warningMessage = $"Do you want to try to delete the Machine Wired LAN profile '{wiredProfile.Name}' from interface '{interfaceToDelete}'?\n\n" +
                                     "Note: Machine/Group Policy profiles may not be deletable via netsh.";
                }
                else
                {
                    // Multiple interfaces - let user choose or delete from all
                    var interfaceList = string.Join("\n", interfaces.Select((name, idx) => $"  {idx + 1}. {name}"));
                    var result = MessageBox.Show(
                        $"The Machine profile '{wiredProfile.Name}' can be applied to multiple interfaces:\n\n{interfaceList}\n\n" +
                        "Click 'Yes' to try to delete from ALL interfaces, or 'No' to cancel.\n\n" +
                        "Note: Machine/Group Policy profiles may not be deletable via netsh.",
                        "SyncML Viewer - Delete Machine Profile",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.No)
                    {
                        return;
                    }

                    // Try to delete from all interfaces and collect results
                    var results = new StringBuilder();
                    bool anySuccess = false;

                    foreach (var ifName in interfaces)
                    {
                        var output = Helper.RunCommandWithResult("netsh", $"lan delete profile interface=\"{ifName}\"", out int exitCode);
                        results.AppendLine($"Interface '{ifName}':");
                        results.AppendLine($"  Exit Code: {exitCode}");
                        if (!string.IsNullOrWhiteSpace(output))
                        {
                            results.AppendLine($"  Result: {output}");
                        }
                        results.AppendLine();

                        if (exitCode == 0)
                        {
                            anySuccess = true;
                        }
                    }

                    // Show results
                    MessageBox.Show(
                        $"Delete operation completed.\n\n{results}",
                        anySuccess ? "SyncML Viewer - Delete Result" : "SyncML Viewer - Delete Failed",
                        MessageBoxButton.OK,
                        anySuccess ? MessageBoxImage.Information : MessageBoxImage.Warning);

                    await RefreshWiredLanProfilesAsync();
                    return;
                }
            }
            else
            {
                // Interface-specific profile (applied instance)
                interfaceToDelete = wiredProfile.InterfaceName ?? wiredProfile.InterfaceGuid;
                warningMessage = $"Do you want to try to delete the Wired LAN profile '{wiredProfile.Name}' from interface '{interfaceToDelete}'?\n\n" +
                                 "Note: Applied/Machine profiles may not be deletable via netsh.\n" +
                                 "To fully remove MDM-deployed profiles, remove the assignment in your MDM and sync the device.";
            }

            var rc = MessageBox.Show(
                warningMessage,
                "SyncML Viewer - Delete Profile",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (rc == MessageBoxResult.No)
            {
                return;
            }

            // Execute the delete command and capture the result
            var deleteOutput = Helper.RunCommandWithResult("netsh", $"lan delete profile interface=\"{interfaceToDelete}\"", out int deleteExitCode);

            // Show the result to the user
            string resultMessage;
            MessageBoxImage icon;

            if (deleteExitCode == 0)
            {
                resultMessage = $"Profile deletion successful.\n\nInterface: {interfaceToDelete}\nExit Code: {deleteExitCode}";
                if (!string.IsNullOrWhiteSpace(deleteOutput))
                {
                    resultMessage += $"\n\nOutput:\n{deleteOutput}";
                }
                icon = MessageBoxImage.Information;
            }
            else
            {
                resultMessage = $"Profile deletion failed.\n\nInterface: {interfaceToDelete}\nExit Code: {deleteExitCode}";
                if (!string.IsNullOrWhiteSpace(deleteOutput))
                {
                    resultMessage += $"\n\nError:\n{deleteOutput}";
                }
                resultMessage += "\n\nNote: Machine/Group Policy profiles cannot be deleted via netsh.\n" +
                                "To remove MDM-deployed profiles, remove the assignment in your MDM and sync the device.";
                icon = MessageBoxImage.Warning;
            }

            MessageBox.Show(
                resultMessage,
                deleteExitCode == 0 ? "SyncML Viewer - Delete Successful" : "SyncML Viewer - Delete Failed",
                MessageBoxButton.OK,
                icon);

            await RefreshWiredLanProfilesAsync();
        }

        private async Task RefreshWiredLanProfilesAsync()
        {
            WiredLanProfileList.Clear();
            TextEditorWiredLanProfiles.Clear();

            // Build a mapping of interface GUID to friendly name
            var guidToName = await Task.Run(() => GetInterfaceGuidToNameMap());

            // Enumerate profiles from both Machine and Interfaces folders
            var profiles = await Task.Run(() => EnumerateWiredLanProfilesFromDisk(guidToName));

            foreach (var p in profiles.OrderBy(p => p.Location).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
            {
                WiredLanProfileList.Add(p);
            }

            ListBoxWiredLan.Items.Refresh();

            if (ListBoxWiredLan.Items.Count > 0)
            {
                ListBoxWiredLan.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// Gets a list of Ethernet interface friendly names
        /// </summary>
        private static List<string> GetEthernetInterfaceNames()
        {
            var result = new List<string>();
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    // Filter for Ethernet adapters (wired)
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                        nic.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet)
                    {
                        result.Add(nic.Name);
                    }
                }
            }
            catch (Exception)
            {
                // ignored
            }
            return result;
        }

        /// <summary>
        /// Builds a dictionary mapping interface GUIDs to friendly names
        /// </summary>
        private static Dictionary<string, string> GetInterfaceGuidToNameMap()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    // The GUID is typically the Id property
                    map[nic.Id] = nic.Name;
                }
            }
            catch (Exception)
            {
                // ignored
            }
            return map;
        }

        private static List<WiredLanProfile> EnumerateWiredLanProfilesFromDisk(Dictionary<string, string> guidToNameMap)
        {
            var result = new List<WiredLanProfile>();
            int machineProfileIndex = 0;
            int interfaceProfileIndex = 0;

            try
            {
                var baseRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Microsoft",
                    "dot3svc",
                    "Profiles");

                if (!Directory.Exists(baseRoot))
                {
                    return result;
                }

                // 1. Enumerate Machine profiles (e.g., from Intune/MDM)
                var machineDir = Path.Combine(baseRoot, "Machine");
                if (Directory.Exists(machineDir))
                {
                    foreach (var file in Directory.EnumerateFiles(machineDir, "*.xml"))
                    {
                        try
                        {
                            var xml = File.ReadAllText(file);
                            var name = GetWiredLanProfileName(xml, file, ref machineProfileIndex);

                            // Machine profiles - deletable via netsh (but may fail for GP/MDM profiles)
                            result.Add(new WiredLanProfile(
                                name, 
                                xml, 
                                WiredLanProfileLocation.Machine, 
                                file,
                                interfaceGuid: null,
                                interfaceName: null,
                                isDeletable: true));
                        }
                        catch (Exception)
                        {
                            // ignore individual file errors
                        }
                    }
                }

                // 2. Enumerate Interface-specific profiles (applied instances)
                var interfacesDir = Path.Combine(baseRoot, "Interfaces");
                if (Directory.Exists(interfacesDir))
                {
                    foreach (var interfaceDir in Directory.EnumerateDirectories(interfacesDir))
                    {
                        var interfaceGuid = new DirectoryInfo(interfaceDir).Name;
                        
                        // Try to get friendly name from the map
                        string interfaceName = null;
                        
                        // The folder name might be the GUID with or without braces
                        var guidVariants = new[] { interfaceGuid, $"{{{interfaceGuid}}}", interfaceGuid.Trim('{', '}') };
                        foreach (var variant in guidVariants)
                        {
                            if (guidToNameMap.TryGetValue(variant, out var friendlyName))
                            {
                                interfaceName = friendlyName;
                                break;
                            }
                        }

                        foreach (var file in Directory.EnumerateFiles(interfaceDir, "*.xml"))
                        {
                            try
                            {
                                var xml = File.ReadAllText(file);
                                var name = GetWiredLanProfileName(xml, file, ref interfaceProfileIndex);

                                // Interface profiles are applied instances - can try to delete but may fail
                                result.Add(new WiredLanProfile(
                                    name,
                                    xml,
                                    WiredLanProfileLocation.Interface,
                                    file,
                                    interfaceGuid,
                                    interfaceName,
                                    isDeletable: true)); // Allow attempt, show result
                            }
                            catch (Exception)
                            {
                                // ignore individual file errors
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // ignore
            }

            return result;
        }

        /// <summary>
        /// Extracts the profile name from the XML, with fallbacks for Intune-deployed profiles
        /// that may not have a name node.
        /// </summary>
        private static string GetWiredLanProfileName(string xml, string filePath, ref int profileIndex)
        {
            // Try to get name from XML first
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(xml);

                // Try standard LANProfile/name path
                var node = doc.SelectSingleNode("/*[local-name()='LANProfile']/*[local-name()='name']");
                if (node != null && !string.IsNullOrWhiteSpace(node.InnerText))
                {
                    return node.InnerText;
                }

                // Try any name element
                node = doc.SelectSingleNode("//*[local-name()='name']");
                if (node != null && !string.IsNullOrWhiteSpace(node.InnerText))
                {
                    return node.InnerText;
                }
            }
            catch (Exception)
            {
                // XML parsing failed, continue to fallbacks
            }

            // Fallback 1: Use filename (without extension)
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            
            // Check if filename looks like a GUID (not very descriptive)
            if (!string.IsNullOrEmpty(fileName) && !IsGuidLike(fileName))
            {
                return fileName;
            }

            // Fallback 2: Generate a descriptive name
            profileIndex++;
            return $"Wired Profile {profileIndex}";
        }

        /// <summary>
        /// Checks if a string looks like a GUID (to avoid using GUIDs as display names)
        /// </summary>
        private static bool IsGuidLike(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            // Remove braces and hyphens, check if remaining chars are hex
            var cleaned = value.Trim('{', '}').Replace("-", "");
            
            // GUIDs are 32 hex characters
            if (cleaned.Length == 32 && cleaned.All(c => Uri.IsHexDigit(c)))
            {
                return true;
            }

            // Also check if it can be parsed as a GUID
            return Guid.TryParse(value, out _);
        }
    }
}
