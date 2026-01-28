using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml;

namespace SyncMLViewer
{
    public partial class MainWindow
    {
        // WiFi profiles
        public List<WifiProfile> WifiProfileList { get; set; }

        private void ListBoxWifi_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (ListBoxWifi.SelectedItem is WifiProfile wifiProfile)
                {
                    TextEditorWifiProfiles.Text = TryFormatXml(wifiProfile.Xml);
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private async void ButtonRefreshWifi_Click(object sender, RoutedEventArgs e)
        {
            WifiProfileList.Clear();

            var output = await Task.Run(() => Helper.RunCommand("netsh", "wlan show interfaces"));
            var guid = Helper.RegexExtractStringValueAfterKeyAndColon(output, "GUID"); // Hopefully this is in every language the same as netsh is localized

            var directoryPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\Microsoft\Wlansvc\Profiles\Interfaces\{" + guid + "}";
            List<XmlDocument> xmlProfiles = Helper.ParseXmlFiles(directoryPath);

            List<WifiProfile> wifiProfiles = new List<WifiProfile>();
            foreach (var xmlProfile in xmlProfiles)
            {
                var name = xmlProfile.SelectSingleNode("/*[local-name()='WLANProfile']/*[local-name()='name']").InnerText;
                wifiProfiles.Add(new WifiProfile(name, xmlProfile.OuterXml));
            }

            foreach (var wifiProfile in wifiProfiles)
            {
                WifiProfileList.Add(new WifiProfile(wifiProfile.Name, wifiProfile.Xml));
            }

            ListBoxWifi.Items.Refresh();
        }

        private void ButtonDeleteWifi_Click(object sender, RoutedEventArgs e)
        {
            if (!(ListBoxWifi.SelectedItem is WifiProfile wifiProfile))
            {
                return;
            }

            var rc = MessageBox.Show($"Do you really want to delete the WiFi profile '{wifiProfile.Name}'?", "SyncML Viewer", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (rc == MessageBoxResult.No)
            {
                return;
            }

            Helper.RunCommand("netsh", $"wlan delete profile name=\"{wifiProfile.Name}\"");

            TextEditorWifiProfiles.Clear();

            ButtonRefreshWifi_Click(null, null);
        }

        private void LabelWifiKey_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!(ListBoxWifi.SelectedItem is WifiProfile wifiProfile))
            {
                return;
            }

            DataEditor dataEditor = new DataEditor
            {
                DataFromMainWindow = wifiProfile.GetKeyContent() ?? "No key material found!",
                HideButonClear = true,
                Title = "Data Editor - WiFi Key",
                TextEditorData = { ShowLineNumbers = false }
            };

            dataEditor.Show();
        }

        private void LabelWifiInfo_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!(ListBoxWifi.SelectedItem is WifiProfile wifiProfile))
            {
                return;
            }

            DataEditor dataEditor = new DataEditor
            {
                DataFromMainWindow = wifiProfile.GetInformation() ?? string.Empty,
                HideButonClear = true,
                Title = "Data Editor - WiFi Information",
                TextEditorData = { ShowLineNumbers = false }
            };

            dataEditor.Show();
        }

        private void LabelBackToTopWifi_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            TextEditorWifiProfiles.ScrollToHome();
        }
    }
}
