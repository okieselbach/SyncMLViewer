using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Management.Automation;

namespace SyncMLViewer
{
    public partial class MainWindow
    {
        // VPN profiles
        public List<VpnProfile> VpnProfileList { get; set; }

        private void ListBoxVpn_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (ListBoxVpn.SelectedItem is VpnProfile vpnProfile)
                {
                    TextEditorVpnProfiles.Text = TryFormatXml(vpnProfile.Xml);
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private async void ButtonRefreshVpn_Click(object sender, RoutedEventArgs e)
        {
            VpnProfileList.Clear();

            using (var ps = PowerShell.Create())
            {
                ps.AddCommand("Get-VpnConnection");

                var psOutput = await Task.Run(() => ps.Invoke());

                foreach (var item in psOutput)
                {
                    var name = item.Members["Name"].Value as string;
                    var xml = item.Members["VpnConfigurationXml"].Value as string;
                    VpnProfileList.Add(new VpnProfile(name, xml));
                }
            }

            ListBoxVpn.Items.Refresh();
        }

        private void ButtonDeleteVpn_Click(object sender, RoutedEventArgs e)
        {
            if (!(ListBoxVpn.SelectedItem is VpnProfile vpnProfile))
            {
                return;
            }

            var rc = MessageBox.Show($"Do you really want to delete the VPN profile '{vpnProfile.Name}'?", "SyncML Viewer", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (rc == MessageBoxResult.No)
            {
                return;
            }

            using (var ps = PowerShell.Create())
            {
                ps.AddCommand("Remove-VpnConnection")
                    .AddParameter("Name", $"{vpnProfile.Name}")
                    .AddParameter("Force");

                ps.Invoke();
            }

            TextEditorVpnProfiles.Clear();

            ButtonRefreshVpn_Click(null, null);
        }

        private void LabelBackToTopVpn_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            TextEditorVpnProfiles.ScrollToHome();
        }
    }
}
