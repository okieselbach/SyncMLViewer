using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private bool _wiredLanProfilesLoadedOnce;

        //private void EnsureWiredLanInitialized()
        //{
        //    if (WiredLanProfileList != null)
        //    {
        //        return;
        //    }

        //    WiredLanProfileList = new List<WiredLanProfile>();

        //    // In case InitializeComponent hasn't wired these yet, guard with null checks.
        //    if (ListBoxWiredLan != null)
        //    {
        //        ListBoxWiredLan.ItemsSource = WiredLanProfileList;
        //    }
        //}

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

        private async void TabItemWiredLan_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            //try
            //{
            //    if (!(sender is TabItem tab)) return;
            //    if (!tab.IsVisible) return;

            //    //EnsureWiredLanInitialized();

            //    if (_wiredLanProfilesLoadedOnce) return;
            //    _wiredLanProfilesLoadedOnce = true;

            //    await RefreshWiredLanProfilesAsync();
            //}
            //catch (Exception)
            //{
            //    // ignored
            //}
        }

        private async void ButtonRefreshWiredLan_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //EnsureWiredLanInitialized();
                await RefreshWiredLanProfilesAsync();
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private async void ButtonDeleteWiredLan_Click(object sender, RoutedEventArgs e)
        {
            //EnsureWiredLanInitialized();

            if (!(ListBoxWiredLan.SelectedItem is WiredLanProfile wiredProfile))
            {
                return;
            }

            var rc = MessageBox.Show(
                $"Do you really want to delete the Wired LAN / 802.1x profile '{wiredProfile.Name}'?",
                "SyncML Viewer",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (rc == MessageBoxResult.No)
            {
                return;
            }

            // Supported deletion method.
            Helper.RunCommand("netsh", $"lan delete profile name=\"{wiredProfile.Name}\"");

            await RefreshWiredLanProfilesAsync();
        }

        private async Task RefreshWiredLanProfilesAsync()
        {
            //EnsureWiredLanInitialized();

            WiredLanProfileList.Clear();
            TextEditorWiredLanProfiles.Clear();

            // Robust enumeration: read dot3svc stored profiles from ProgramData.
            var profiles = await Task.Run(EnumerateWiredLanProfilesFromDisk);

            foreach (var p in profiles.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
            {
                WiredLanProfileList.Add(p);
            }

            ListBoxWiredLan.Items.Refresh();

            if (ListBoxWiredLan.Items.Count > 0)
            {
                ListBoxWiredLan.SelectedIndex = 0;
            }
        }

        private static List<WiredLanProfile> EnumerateWiredLanProfilesFromDisk()
        {
            var result = new List<WiredLanProfile>();

            try
            {
                var root = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Microsoft",
                    "dot3svc",
                    "Profiles",
                    "Interfaces");

                if (!Directory.Exists(root))
                {
                    return result;
                }

                foreach (var interfaceDir in Directory.EnumerateDirectories(root))
                {
                    foreach (var file in Directory.EnumerateFiles(interfaceDir, "*.xml"))
                    {
                        try
                        {
                            var xml = File.ReadAllText(file);
                            var name = GetWiredLanProfileName(xml) ?? Path.GetFileNameWithoutExtension(file);

                            // Disambiguate duplicates across interfaces.
                            var interfaceName = new DirectoryInfo(interfaceDir).Name;
                            var displayName = name;
                            if (result.Any(x => string.Equals(x.Name, displayName, StringComparison.OrdinalIgnoreCase)))
                            {
                                displayName = $"{name} ({interfaceName})";
                            }

                            result.Add(new WiredLanProfile(displayName, xml));
                        }
                        catch (Exception)
                        {
                            // ignore
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

        private static string GetWiredLanProfileName(string xml)
        {
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(xml);

                var node = doc.SelectSingleNode("/*[local-name()='LANProfile']/*[local-name()='name']")
                           ?? doc.SelectSingleNode("//*[local-name()='name']");

                return node?.InnerText;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
