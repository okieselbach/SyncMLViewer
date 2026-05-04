using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
        private bool _wnsFilterByEventId = false;
        private HashSet<int> _wnsFilterEventIds = new HashSet<int>();
        private bool _wnsShowDecodedPayload = true;

        // WNS provider filter settings (which providers to capture)
        private HashSet<Guid> _wnsEnabledProviders = new HashSet<Guid>
        {
            WnsPushNotificationsPlatform
        };

        // ETW parser settings
        private bool _wnsUseDynamicAll = true;
        private bool _wnsUseUnhandledEvents = true;

        private const string WnsSessionName = "SyncMLViewer-WnsListener";
        private BackgroundWorker _wnsBackgroundWorker;
        private TraceEventSession _wnsTraceEventSession;

        /// <summary>
        /// Checks if the given provider GUID is a WNS provider and currently enabled.
        /// </summary>
        private bool IsWnsProvider(Guid providerGuid)
        {
            return WnsProviderNames.ContainsKey(providerGuid) && _wnsEnabledProviders.Contains(providerGuid);
        }

        /// <summary>
        /// Starts the dedicated WNS ETW session.
        /// </summary>
        private void StartWnsSession()
        {
            if (_wnsBackgroundWorker != null && _wnsBackgroundWorker.IsBusy)
                return;

            if (_wnsEnabledProviders.Count == 0)
                return;

            _wnsBackgroundWorker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            _wnsBackgroundWorker.DoWork += WnsWorkerDoWork;
            _wnsBackgroundWorker.ProgressChanged += WnsWorkerProgressChanged;
            _wnsBackgroundWorker.RunWorkerAsync();
        }

        /// <summary>
        /// Stops the dedicated WNS ETW session.
        /// </summary>
        private void StopWnsSession()
        {
            try
            {
                _wnsTraceEventSession?.Stop();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error stopping WNS session: {ex.Message}");
            }
            _wnsTraceEventSession = null;
        }

        /// <summary>
        /// Restarts the WNS session (e.g. after provider/parser changes).
        /// </summary>
        private void RestartWnsSession()
        {
            StopWnsSession();
            if (_wnsTraceEnabled)
            {
                StartWnsSession();
            }
        }

        /// <summary>
        /// Cleans up the WNS session on window close.
        /// </summary>
        internal void CleanupWnsSession()
        {
            StopWnsSession();
        }

        private void WnsWorkerDoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                if (TraceEventSession.IsElevated() != true)
                {
                    Debug.WriteLine("WNS Listener: Administrative privileges required.");
                    return;
                }

                if (TraceEventSession.GetActiveSessionNames().Contains(WnsSessionName))
                {
                    Debug.WriteLine($"WNS Listener: Session '{WnsSessionName}' already active, stopping it.");
                    TraceEventSession.GetActiveSession(WnsSessionName).Stop(true);
                }

                using (var session = new TraceEventSession(WnsSessionName))
                {
                    _wnsTraceEventSession = session;
                    session.StopOnDispose = true;

                    using (var source = new ETWTraceEventSource(WnsSessionName, TraceEventSourceType.Session))
                    {
                        // Enable all configured WNS providers
                        foreach (var providerGuid in _wnsEnabledProviders)
                        {
                            session.EnableProvider(providerGuid, TraceEventLevel.Verbose, 0xFFFFFFFFFFFFFFFF);
                        }

                        var reportProgress = (Action<TraceEvent>)(data =>
                            (sender as BackgroundWorker)?.ReportProgress(0, data.Clone()));

                        new RegisteredTraceEventParser(source).All += reportProgress;
                        if (_wnsUseDynamicAll)
                            source.Dynamic.All += reportProgress;
                        if (_wnsUseUnhandledEvents)
                            source.UnhandledEvents += reportProgress;

                        source.Process();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WNS Listener exception: {ex}");
            }
            finally
            {
                _wnsTraceEventSession = null;
            }
        }

        private void WnsWorkerProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (!(e.UserState is TraceEvent data))
                return;

            if (IsWnsProvider(data.ProviderGuid))
            {
                ProcessWnsEvent(data);
            }
        }

        /// <summary>
        /// Processes a WNS ETW event and adds it to the WnsMessages collection.
        /// </summary>
        // Cache event IDs that throw FormatException to avoid repeated exceptions
        private readonly HashSet<int> _wnsEventIdsWithFormatErrors = new HashSet<int>();

        private void ProcessWnsEvent(TraceEvent data)
        {
            try
            {
                int eventId = (int)data.ID;
                string providerName = WnsProviderNames.GetValueOrDefault(data.ProviderGuid, data.ProviderName ?? "Unknown");
                bool hasFormatErrors = _wnsEventIdsWithFormatErrors.Contains(eventId);

                // Use event ID directly to avoid FormatException from EventName
                string eventName = $"EventID({eventId})";
                if (!hasFormatErrors)
                {
                    try
                    {
                        string name = data.EventName;
                        if (!string.IsNullOrEmpty(name))
                            eventName = name;
                    }
                    catch (FormatException)
                    {
                        _wnsEventIdsWithFormatErrors.Add(eventId);
                    }
                }

                var wnsMessage = new WnsMessage(
                    eventId,
                    eventName,
                    providerName,
                    data.ProviderGuid)
                {
                    Timestamp = data.TimeStamp
                };

                // Skip FormattedMessage for events known to throw - it's rarely useful anyway
                if (!hasFormatErrors)
                {
                    try
                    {
                        wnsMessage.FormattedMessage = data.FormattedMessage;
                    }
                    catch (FormatException)
                    {
                        _wnsEventIdsWithFormatErrors.Add(eventId);
                    }
                }

                // Extract payload fields
                string[] payloadNames = null;
                try
                {
                    payloadNames = data.PayloadNames;
                }
                catch (FormatException)
                {
                    _wnsEventIdsWithFormatErrors.Add(eventId);
                }

                if (payloadNames != null)
                {
                    for (int i = 0; i < payloadNames.Length; i++)
                    {
                        string name = payloadNames[i];
                        object val;
                        try
                        {
                            val = data.PayloadValue(i);
                        }
                        catch (FormatException)
                        {
                            continue;
                        }

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
                }

                // Try to decode hex payload from any event that has a "Payload" field
                string payloadKey = wnsMessage.PayloadFields.Keys
                    .FirstOrDefault(k => k.Equals("Payload", StringComparison.OrdinalIgnoreCase));

                if (payloadKey != null && LooksLikeHexPayload(wnsMessage.PayloadFields[payloadKey]))
                {
                    string hexValue = wnsMessage.PayloadFields[payloadKey];
                    wnsMessage.RawPayloadHex = hexValue;
                    string decoded = TryDecodeHexPayload(hexValue);
                    if (decoded != null)
                    {
                        wnsMessage.DecodedPayload = decoded;
                    }
                }

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

                    // Show decoded hex payload
                    if (kvp.Key.Equals("Payload", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrEmpty(message.DecodedPayload))
                    {
                        value = $"{value}\n  >> Decoded Payload:\n{message.DecodedPayload}";
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
        /// Checks if a string looks like a hex payload (e.g. "50 4E 47 20 ... (71 bytes)" or "0x504E4720...").
        /// </summary>
        private bool LooksLikeHexPayload(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length < 4)
                return false;

            // BitConverter format: "XX XX XX ... (N bytes)"
            if (Regex.IsMatch(value, @"\(\d+\s+bytes?\)\s*$", RegexOptions.IgnoreCase))
                return true;

            // 0x-prefixed hex
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                Regex.IsMatch(value.Substring(2).Trim(), @"^[0-9A-Fa-f]+$"))
                return true;

            return false;
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

        /// <summary>
        /// Tries to decode a hex payload string to UTF-8 text, optionally pretty-printing JSON.
        /// Handles both "0x..." format and "XX XX XX (N bytes)" BitConverter format.
        /// </summary>
        private string TryDecodeHexPayload(string hexValue)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(hexValue))
                    return null;

                string clean = hexValue.Trim();

                // Strip "0x" prefix
                if (clean.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    clean = clean.Substring(2);

                // Strip trailing " (N bytes)" suffix from BitConverter format
                var byteSuffixMatch = Regex.Match(clean, @"\s+\(\d+\s+bytes?\)\s*$", RegexOptions.IgnoreCase);
                if (byteSuffixMatch.Success)
                    clean = clean.Substring(0, byteSuffixMatch.Index);

                // Remove spaces and dashes (BitConverter uses "XX-XX" or "XX XX")
                clean = clean.Replace(" ", "").Replace("-", "");

                if (string.IsNullOrEmpty(clean) || clean.Length % 2 != 0)
                    return null;

                // Validate hex characters
                if (!Regex.IsMatch(clean, @"^[0-9A-Fa-f]+$"))
                    return null;

                byte[] bytes = new byte[clean.Length / 2];
                for (int i = 0; i < clean.Length; i += 2)
                {
                    bytes[i / 2] = Convert.ToByte(clean.Substring(i, 2), 16);
                }

                string decoded = Encoding.UTF8.GetString(bytes);

                // Strip null characters (used as field separators in some WNS payloads)
                decoded = decoded.Replace("\0", "");

                if (string.IsNullOrWhiteSpace(decoded))
                    return null;

                // Check that most characters are readable text (allow some control chars)
                int printable = decoded.Count(c => !char.IsControl(c) || c == '\n' || c == '\r' || c == '\t');
                if (printable < decoded.Length * 0.8)
                    return null;

                return PrettyFormatDecodedPayload(decoded);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Pretty-formats decoded payload text. Handles:
        /// - Pure JSON (pretty-printed with nested JSON expansion)
        /// - Pure XML (indented)
        /// - Headers + body (headers preserved, body pretty-printed)
        /// </summary>
        private string PrettyFormatDecodedPayload(string decoded)
        {
            if (string.IsNullOrWhiteSpace(decoded))
                return decoded;

            var trimmed = decoded.Trim();

            // Try pure JSON
            string json = TryPrettyPrintJson(trimmed);
            if (json != null) return json;

            // Try pure XML
            if (LooksLikeXml(trimmed))
            {
                string xml = TryFormatWnsXml(trimmed);
                if (xml != trimmed) return xml.TrimStart('\n');
            }

            // Find embedded JSON object/array in the text (look for last { or [ that starts a valid JSON)
            int jsonStart = FindJsonStart(trimmed);
            if (jsonStart > 0)
            {
                string before = trimmed.Substring(0, jsonStart).TrimEnd();
                string jsonPart = trimmed.Substring(jsonStart);
                string prettyJson = TryPrettyPrintJson(jsonPart);

                if (prettyJson != null)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine(before);
                    sb.AppendLine();
                    sb.Append(prettyJson);
                    return sb.ToString();
                }
            }

            // Find embedded XML in the text
            int xmlStart = trimmed.IndexOf('<');
            if (xmlStart > 0)
            {
                string before = trimmed.Substring(0, xmlStart).TrimEnd();
                string xmlPart = trimmed.Substring(xmlStart);
                if (LooksLikeXml(xmlPart))
                {
                    string prettyXml = TryFormatWnsXml(xmlPart);
                    if (prettyXml != xmlPart)
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine(before);
                        sb.AppendLine();
                        sb.Append(prettyXml.TrimStart('\n'));
                        return sb.ToString();
                    }
                }
            }

            return decoded;
        }

        /// <summary>
        /// Finds the start index of an embedded JSON object/array in a string.
        /// Scans forward to find the first { or [ whose JSON extends to the end of the string.
        /// </summary>
        private int FindJsonStart(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '{' || c == '[')
                {
                    string candidate = text.Substring(i).TrimEnd();
                    try
                    {
                        using (var reader = new Newtonsoft.Json.JsonTextReader(new System.IO.StringReader(candidate)))
                        {
                            JToken.Load(reader);
                            // Ensure the JSON consumes the entire remaining text (no trailing content)
                            if (!reader.Read())
                                return i;
                        }
                    }
                    catch { }
                }
            }
            return -1;
        }

        /// <summary>
        /// Tries to parse and pretty-print JSON, expanding nested escaped JSON strings.
        /// </summary>
        private string TryPrettyPrintJson(string text)
        {
            try
            {
                var token = JToken.Parse(text);
                ExpandNestedJson(token);
                return token.ToString(Newtonsoft.Json.Formatting.Indented);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Recursively expands string values that contain escaped JSON into actual JSON objects.
        /// </summary>
        private void ExpandNestedJson(JToken token)
        {
            if (token is JObject obj)
            {
                var properties = obj.Properties().ToList();
                foreach (var prop in properties)
                {
                    if (prop.Value.Type == JTokenType.String)
                    {
                        string strVal = prop.Value.Value<string>();
                        if (!string.IsNullOrEmpty(strVal) && (strVal.TrimStart().StartsWith("{") || strVal.TrimStart().StartsWith("[")))
                        {
                            try
                            {
                                var nested = JToken.Parse(strVal);
                                ExpandNestedJson(nested);
                                prop.Value = nested;
                            }
                            catch { }
                        }
                    }
                    else
                    {
                        ExpandNestedJson(prop.Value);
                    }
                }
            }
            else if (token is JArray arr)
            {
                for (int i = 0; i < arr.Count; i++)
                {
                    if (arr[i].Type == JTokenType.String)
                    {
                        string strVal = arr[i].Value<string>();
                        if (!string.IsNullOrEmpty(strVal) && (strVal.TrimStart().StartsWith("{") || strVal.TrimStart().StartsWith("[")))
                        {
                            try
                            {
                                var nested = JToken.Parse(strVal);
                                ExpandNestedJson(nested);
                                arr[i] = nested;
                            }
                            catch { }
                        }
                    }
                    else
                    {
                        ExpandNestedJson(arr[i]);
                    }
                }
            }
        }

        /// <summary>
        /// Initializes ICollectionView filtering for the WnsMessages collection.
        /// </summary>
        internal void InitWnsCollectionView()
        {
            var view = CollectionViewSource.GetDefaultView(WnsMessages);
            view.Filter = WnsEventFilter;
        }

        private bool WnsEventFilter(object item)
        {
            if (!_wnsFilterByEventId || _wnsFilterEventIds.Count == 0)
                return true;
            return item is WnsMessage msg && _wnsFilterEventIds.Contains(msg.EventId);
        }

        private void ParseWnsFilterEventIds()
        {
            _wnsFilterEventIds.Clear();
            if (TextBoxWnsFilterEventIds == null)
                return;

            foreach (var part in TextBoxWnsFilterEventIds.Text.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(part.Trim(), out int id))
                {
                    _wnsFilterEventIds.Add(id);
                }
            }
        }

        #region UI Event Handlers

        private void ListBoxWnsMessages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListBoxWnsMessages.SelectedItem is WnsMessage wnsMessage)
            {
                TextEditorWnsDetails.Text = wnsMessage.GetFormattedPayload(_wnsShowDecodedPayload);
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
                StartWnsSession();
            }
            else
            {
                ImageWnsCapture.Visibility = Visibility.Hidden;
                StopWnsSession();
            }
        }

        private void LabelBackToTopWns_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            TextEditorWnsStream.ScrollToHome();
        }

        private void ButtonWnsOptions_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new WnsOptionsDialog(
                _wnsEnabledProviders,
                WnsProviderNames,
                _wnsUseDynamicAll,
                _wnsUseUnhandledEvents)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                _wnsEnabledProviders = dialog.EnabledProviders;
                _wnsUseDynamicAll = dialog.UseDynamicAll;
                _wnsUseUnhandledEvents = dialog.UseUnhandledEvents;

                // Restart session with new settings if capture is active
                if (_wnsTraceEnabled)
                {
                    RestartWnsSession();
                }
            }
        }

        private void CheckBoxWnsFilterEvents_Changed(object sender, RoutedEventArgs e)
        {
            _wnsFilterByEventId = CheckBoxWnsFilterEvents.IsChecked == true;
            ParseWnsFilterEventIds();
            CollectionViewSource.GetDefaultView(WnsMessages)?.Refresh();
        }

        private void TextBoxWnsFilterEventIds_TextChanged(object sender, TextChangedEventArgs e)
        {
            ParseWnsFilterEventIds();
            if (_wnsFilterByEventId)
            {
                CollectionViewSource.GetDefaultView(WnsMessages)?.Refresh();
            }
        }

        private void LabelToggleWnsPayload_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _wnsShowDecodedPayload = !_wnsShowDecodedPayload;
            LabelToggleWnsPayload.Content = _wnsShowDecodedPayload ? "[Show Raw]" : "[Show Decoded]";

            if (ListBoxWnsMessages.SelectedItem is WnsMessage wnsMessage)
            {
                TextEditorWnsDetails.Text = wnsMessage.GetFormattedPayload(_wnsShowDecodedPayload);
            }
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
