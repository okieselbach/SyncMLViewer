using Microsoft.Diagnostics.Tracing;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;

namespace SyncMLViewer
{
    /// <summary>
    /// Partial class for WNS (Windows Notification Service) ETW monitoring functionality.
    /// </summary>
    public partial class MainWindow
    {
        // WNS-related ETW provider GUIDs
        // Microsoft-Windows-PushNotifications-Platform
        private static readonly Guid WnsPushNotificationsPlatform = new Guid("{88CD9180-4491-4640-B571-E3BEE2527943}");
        // Microsoft-Windows-PushNotifications-Developer
        private static readonly Guid WnsPushNotificationsDeveloper = new Guid("{5CAD3597-5FEC-4C62-9CE1-9D7ABC723D3A}");
        // Microsoft-Windows-PushNotifications-InProc
        private static readonly Guid WnsPushNotificationsInProc = new Guid("{815A1F4A-3F8D-4B37-9B31-5142F9D724A5}");
        // Microsoft-Windows-DeviceManagement-Pushrouter (MDM push routing)
        private static readonly Guid WnsMdmPushRouter = new Guid("{F1201B5A-E170-42B6-8D20-B57AC57E6416}");

        // Provider names for display
        private static readonly Dictionary<Guid, string> WnsProviderNames = new Dictionary<Guid, string>
        {
            [WnsPushNotificationsPlatform] = "PushNotifications-Platform",
            [WnsPushNotificationsDeveloper] = "PushNotifications-Developer",
            [WnsPushNotificationsInProc] = "PushNotifications-InProc",
            [WnsMdmPushRouter] = "DeviceManagement-Pushrouter"
        };

        // Fields that likely contain notification payload / message content
        private static readonly HashSet<string> WnsPayloadFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Payload", "PayloadContent", "Content", "NotificationContent",
            "NotificationPayload", "RawPayload", "RawContent", "Body",
            "Message", "Data", "XmlPayload", "ToastXml", "TileXml",
            "BadgeXml", "RawNotification", "ChannelUri", "ChannelId",
            "AppId", "AppUserModelId", "NotificationType", "Tag",
            "Group", "Status", "StatusCode", "ErrorCode", "Uri",
            "Url", "PackageFamilyName", "Result", "HResult"
        };

        /// <summary>
        /// Collection of WNS messages for the ListBox binding.
        /// </summary>
        public ObservableCollection<WnsMessage> WnsMessages { get; } = new ObservableCollection<WnsMessage>();

        private bool _wnsTraceEnabled = false;

        /// <summary>
        /// Checks if the given provider GUID is a WNS provider.
        /// </summary>
        private bool IsWnsProvider(Guid providerGuid)
        {
            return WnsProviderNames.ContainsKey(providerGuid);
        }

        /// <summary>
        /// Processes a WNS ETW event and adds it to the WnsMessages collection.
        /// </summary>
        private void ProcessWnsEvent(TraceEvent data)
        {
            try
            {
                string providerName = WnsProviderNames.GetValueOrDefault(data.ProviderGuid, data.ProviderName ?? "Unknown");

                var wnsMessage = new WnsMessage(
                    (int)data.ID,
                    data.EventName ?? $"EventID_{(int)data.ID}",
                    providerName,
                    data.ProviderGuid)
                {
                    Timestamp = data.TimeStamp,
                    FormattedMessage = data.FormattedMessage
                };

                // Extract payload fields
                for (int i = 0; i < data.PayloadNames.Length; i++)
                {
                    string name = data.PayloadNames[i];
                    object val = data.PayloadValue(i);
                    string valStr;

                    if (val is byte[] bytes)
                    {
                        valStr = BitConverter.ToString(bytes).Replace("-", " ") + $" ({bytes.Length} bytes)";
                    }
                    else if (val == null)
                    {
                        valStr = "(null)";
                    }
                    else
                    {
                        valStr = val.ToString() ?? "(empty)";
                    }

                    if (!string.IsNullOrWhiteSpace(valStr))
                    {
                        wnsMessage.PayloadFields[name] = valStr;
                    }
                }

                // Update UI on dispatcher thread
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    WnsMessages.Add(wnsMessage);

                    // Auto-scroll if enabled
                    if (menuItemAutoScroll.IsChecked && ListBoxWnsMessages.Items.Count > 0)
                    {
                        ListBoxWnsMessages.ScrollIntoView(ListBoxWnsMessages.Items[ListBoxWnsMessages.Items.Count - 1]);
                    }

                    // Update stream view if WNS tab is selected
                    if (TabItemWns.IsSelected)
                    {
                        AppendWnsMessageToStream(wnsMessage);
                    }
                }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing WNS event: {ex.Message}");
            }
        }

        /// <summary>
        /// Appends a WNS message to the stream text editor.
        /// </summary>
        private void AppendWnsMessageToStream(WnsMessage message)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"<!-- [{message.Timestamp:HH:mm:ss.fff}] {message.ProviderName} - {message.EventName} (ID: {message.EventId}) -->");

            if (message.PayloadFields.Count > 0)
            {
                foreach (var kvp in message.PayloadFields)
                {
                    string value = kvp.Value;

                    // Try to format XML content
                    if (IsPayloadField(kvp.Key) && LooksLikeXml(value))
                    {
                        value = TryFormatWnsXml(value);
                    }

                    // Try to decode base64
                    if (IsPayloadField(kvp.Key) && LooksLikeBase64(value))
                    {
                        string decoded = TryDecodeBase64(value);
                        if (decoded != null)
                        {
                            value = $"{value}\n  >> Decoded: {decoded}";
                        }
                    }

                    sb.AppendLine($"  {kvp.Key}: {value}");
                }
            }
            else if (!string.IsNullOrEmpty(message.FormattedMessage))
            {
                sb.AppendLine($"  Message: {message.FormattedMessage}");
            }

            sb.AppendLine();

            TextEditorWnsStream.AppendText(sb.ToString());

            if (menuItemAutoScroll.IsChecked)
            {
                TextEditorWnsStream.ScrollToEnd();
            }
        }

        /// <summary>
        /// Checks if a field name is likely to contain payload content.
        /// </summary>
        private bool IsPayloadField(string fieldName)
        {
            return WnsPayloadFieldNames.Contains(fieldName);
        }

        /// <summary>
        /// Checks if a string looks like XML.
        /// </summary>
        private bool LooksLikeXml(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var trimmed = value.TrimStart();
            return trimmed.StartsWith("<") && trimmed.Contains(">");
        }

        /// <summary>
        /// Checks if a string looks like Base64 encoded data.
        /// </summary>
        private bool LooksLikeBase64(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length < 8 || value.Contains(' ') || value.Contains('<'))
                return false;

            int validChars = value.Count(c => char.IsLetterOrDigit(c) || c == '+' || c == '/' || c == '=');
            return validChars > value.Length * 0.9;
        }

        /// <summary>
        /// Tries to decode a Base64 string.
        /// </summary>
        private string TryDecodeBase64(string value)
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(value);
                string decoded = Encoding.UTF8.GetString(bytes);

                // Only return if it looks like readable text
                if (decoded.All(c => !char.IsControl(c) || c == '\n' || c == '\r' || c == '\t'))
                    return decoded;

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Tries to format XML for better readability.
        /// </summary>
        private string TryFormatWnsXml(string xml)
        {
            try
            {
                var doc = XDocument.Parse(xml);
                return "\n" + string.Join("\n", doc.ToString()
                    .Split('\n')
                    .Select(line => "    " + line));
            }
            catch
            {
                return xml;
            }
        }

        #region UI Event Handlers

        private void ListBoxWnsMessages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListBoxWnsMessages.SelectedItem is WnsMessage wnsMessage)
            {
                TextEditorWnsDetails.Text = wnsMessage.GetFormattedPayload();
            }
        }

        private void ButtonClearWns_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Do you really want to clear all WNS messages?", "Clear WNS Messages", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                WnsMessages.Clear();
                TextEditorWnsStream.Clear();
                TextEditorWnsDetails.Clear();
            }
        }

        private void ButtonSaveWns_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Text files|*.txt|All files|*.*",
                FilterIndex = 0,
                DefaultExt = "txt",
                AddExtension = true,
                CheckPathExists = true,
                RestoreDirectory = true,
                Title = "Save WNS Stream",
                FileName = $"WNS-Stream-{Environment.MachineName}-{DateTime.Now:MM-dd-yy_H-mm-ss}.txt"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                System.IO.File.WriteAllText(saveFileDialog.FileName, TextEditorWnsStream.Text);
            }
        }

        private void CheckBoxWnsCapture_Changed(object sender, RoutedEventArgs e)
        {
            _wnsTraceEnabled = CheckBoxWnsCapture.IsChecked == true;

            if (_wnsTraceEnabled)
            {
                ImageWnsCapture.Visibility = Visibility.Visible;
            }
            else
            {
                ImageWnsCapture.Visibility = Visibility.Hidden;
            }
        }

        private void LabelBackToTopWns_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            TextEditorWnsStream.ScrollToHome();
        }

        private void ButtonTestWns_Click(object sender, RoutedEventArgs e)
        {
            // Generate a test WNS message to verify the UI is working
            var testMessage = new WnsMessage(
                9999,
                "TestEvent",
                "Test-Provider",
                Guid.NewGuid())
            {
                Timestamp = DateTime.Now,
                FormattedMessage = "This is a test WNS event to verify the UI is working correctly."
            };

            testMessage.PayloadFields["ChannelUri"] = "https://wns.windows.com/test-channel-uri";
            testMessage.PayloadFields["AppId"] = "Microsoft.CompanyPortal_8wekyb3d8bbwe";
            testMessage.PayloadFields["NotificationType"] = "Raw";
            testMessage.PayloadFields["Payload"] = "Test payload content - in real scenarios this would contain MDM sync triggers or other notification data";
            testMessage.PayloadFields["Status"] = "Success";
            testMessage.PayloadFields["Timestamp"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

            WnsMessages.Add(testMessage);
            AppendWnsMessageToStream(testMessage);

            MessageBox.Show(
                "A test WNS event has been added to the list.\n\n" +
                "To capture real WNS events:\n" +
                "1. Check 'Capture WNS Traffic'\n" +
                "2. Trigger an MDM sync or wait for push notifications\n" +
                "3. WNS events will appear in the list\n\n" +
                "Note: WNS events are generated when:\n" +
                "- Intune sends push notifications to the device\n" +
                "- Company Portal or other WNS-enabled apps receive notifications\n" +
                "- Windows receives sync triggers from MDM",
                "WNS Test Event",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        #endregion
    }

    /// <summary>
    /// Extension method for Dictionary to provide GetValueOrDefault for .NET Framework 4.7.2
    /// </summary>
    public static class DictionaryExtensions
    {
        public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default)
        {
            return dictionary.TryGetValue(key, out TValue value) ? value : defaultValue;
        }
    }
}
