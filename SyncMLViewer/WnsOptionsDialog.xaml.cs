using System;
using System.Collections.Generic;
using System.Windows;

namespace SyncMLViewer
{
    public partial class WnsOptionsDialog : Window
    {
        private static readonly Guid WnsPushNotificationsPlatform = new Guid("{88CD9180-4491-4640-B571-E3BEE2527943}");
        private static readonly Guid WnsPushNotificationsDeveloper = new Guid("{5CAD3597-5FEC-4C62-9CE1-9D7ABC723D3A}");
        private static readonly Guid WnsPushNotificationsInProc = new Guid("{815A1F4A-3F8D-4B37-9B31-5142F9D724A5}");
        private static readonly Guid WnsMdmPushRouter = new Guid("{F1201B5A-E170-42B6-8D20-B57AC57E6416}");

        public HashSet<Guid> EnabledProviders { get; private set; }
        public bool UseDynamicAll { get; private set; }
        public bool UseUnhandledEvents { get; private set; }

        public WnsOptionsDialog(
            HashSet<Guid> enabledProviders,
            Dictionary<Guid, string> providerNames,
            bool useDynamicAll,
            bool useUnhandledEvents)
        {
            InitializeComponent();

            CheckBoxPlatform.IsChecked = enabledProviders.Contains(WnsPushNotificationsPlatform);
            CheckBoxDeveloper.IsChecked = enabledProviders.Contains(WnsPushNotificationsDeveloper);
            CheckBoxInProc.IsChecked = enabledProviders.Contains(WnsPushNotificationsInProc);
            CheckBoxPushRouter.IsChecked = enabledProviders.Contains(WnsMdmPushRouter);

            CheckBoxDynamicAll.IsChecked = useDynamicAll;
            CheckBoxUnhandledEvents.IsChecked = useUnhandledEvents;
        }

        private void ButtonOk_Click(object sender, RoutedEventArgs e)
        {
            EnabledProviders = new HashSet<Guid>();

            if (CheckBoxPlatform.IsChecked == true)
                EnabledProviders.Add(WnsPushNotificationsPlatform);
            if (CheckBoxDeveloper.IsChecked == true)
                EnabledProviders.Add(WnsPushNotificationsDeveloper);
            if (CheckBoxInProc.IsChecked == true)
                EnabledProviders.Add(WnsPushNotificationsInProc);
            if (CheckBoxPushRouter.IsChecked == true)
                EnabledProviders.Add(WnsMdmPushRouter);

            UseDynamicAll = CheckBoxDynamicAll.IsChecked == true;
            UseUnhandledEvents = CheckBoxUnhandledEvents.IsChecked == true;

            DialogResult = true;
        }
    }
}
