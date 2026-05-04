using System;
using System.Collections.Generic;
using System.Windows;

namespace SyncMLViewer
{
    public partial class EtwProviderDialog : Window
    {
        public List<Guid> ProviderGuids { get; private set; }
        public string ProviderText { get; private set; }
        public bool UseDynamicAll { get; private set; }
        public bool UseUnhandledEvents { get; private set; }

        public EtwProviderDialog(
            string providerText,
            bool useDynamicAll,
            bool useUnhandledEvents)
        {
            InitializeComponent();

            TextBoxProviders.Text = providerText;
            CheckBoxEtwDynamicAll.IsChecked = useDynamicAll;
            CheckBoxEtwUnhandledEvents.IsChecked = useUnhandledEvents;
        }

        private void ButtonOk_Click(object sender, RoutedEventArgs e)
        {
            ProviderGuids = new List<Guid>();
            ProviderText = TextBoxProviders.Text;

            foreach (var line in TextBoxProviders.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                    continue;

                // Remove braces if present
                trimmed = trimmed.Trim('{', '}', ' ');

                if (Guid.TryParse(trimmed, out Guid guid))
                {
                    ProviderGuids.Add(guid);
                }
                else
                {
                    MessageBox.Show($"Invalid GUID format: {line.Trim()}\n\nExpected format: {{XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX}}",
                        "Invalid Provider GUID", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            UseDynamicAll = CheckBoxEtwDynamicAll.IsChecked == true;
            UseUnhandledEvents = CheckBoxEtwUnhandledEvents.IsChecked == true;

            DialogResult = true;
        }
    }
}
