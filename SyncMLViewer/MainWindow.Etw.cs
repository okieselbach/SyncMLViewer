using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace SyncMLViewer
{
    /// <summary>
    /// Partial class for generic ETW listener functionality.
    /// Uses its own dedicated ETW session separate from the SyncML/WNS session.
    /// </summary>
    public partial class MainWindow
    {
        private const string EtwSessionName = "SyncMLViewer-EtwListener";

        /// <summary>
        /// Collection of generic ETW messages for the ListBox binding.
        /// </summary>
        public ObservableCollection<EtwMessage> EtwMessages { get; } = new ObservableCollection<EtwMessage>();

        private bool _etwTraceEnabled = false;
        private BackgroundWorker _etwBackgroundWorker;
        private TraceEventSession _etwTraceEventSession;

        // User-configurable ETW providers
        private List<Guid> _etwCustomProviders = new List<Guid>();

        // ETW parser settings for generic listener
        private bool _etwUseDynamicAll = true;
        private bool _etwUseUnhandledEvents = true;

        // Provider text for the dialog (persisted between opens)
        private string _etwProviderText =
            "# Generic ETW Listener - Add provider GUIDs below (one per line)\r\n" +
            "# Lines starting with # are comments\r\n" +
            "#\r\n" +
            "# Example: IME SideCar provider\r\n" +
            "{e20927af-32d7-4d5d-9f73-82f077a1c891}\r\n";

        /// <summary>
        /// Starts the dedicated ETW session for the generic listener.
        /// </summary>
        private void StartEtwSession()
        {
            if (_etwBackgroundWorker != null && _etwBackgroundWorker.IsBusy)
                return;

            if (_etwCustomProviders.Count == 0)
                return;

            _etwBackgroundWorker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            _etwBackgroundWorker.DoWork += EtwWorkerDoWork;
            _etwBackgroundWorker.ProgressChanged += EtwWorkerProgressChanged;
            _etwBackgroundWorker.RunWorkerAsync();
        }

        /// <summary>
        /// Stops the dedicated ETW session.
        /// </summary>
        private void StopEtwSession()
        {
            try
            {
                _etwTraceEventSession?.Stop();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error stopping ETW session: {ex.Message}");
            }
            _etwTraceEventSession = null;
        }

        /// <summary>
        /// Restarts the ETW session (e.g. after provider changes).
        /// </summary>
        private void RestartEtwSession()
        {
            StopEtwSession();
            if (_etwTraceEnabled)
            {
                StartEtwSession();
            }
        }

        private void EtwWorkerDoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                if (TraceEventSession.IsElevated() != true)
                {
                    Debug.WriteLine("ETW Listener: Administrative privileges required.");
                    return;
                }

                if (TraceEventSession.GetActiveSessionNames().Contains(EtwSessionName))
                {
                    Debug.WriteLine($"ETW Listener: Session '{EtwSessionName}' already active, stopping it.");
                    TraceEventSession.GetActiveSession(EtwSessionName).Stop(true);
                }

                using (var session = new TraceEventSession(EtwSessionName))
                {
                    _etwTraceEventSession = session;
                    session.StopOnDispose = true;

                    using (var source = new ETWTraceEventSource(EtwSessionName, TraceEventSourceType.Session))
                    {
                        // Enable all user-configured providers
                        foreach (var providerGuid in _etwCustomProviders)
                        {
                            session.EnableProvider(providerGuid, TraceEventLevel.Verbose, 0xFFFFFFFFFFFFFFFF);
                        }

                        var reportProgress = (Action<TraceEvent>)(data =>
                            (sender as BackgroundWorker)?.ReportProgress(0, data.Clone()));

                        new RegisteredTraceEventParser(source).All += reportProgress;
                        if (_etwUseDynamicAll)
                            source.Dynamic.All += reportProgress;
                        if (_etwUseUnhandledEvents)
                            source.UnhandledEvents += reportProgress;

                        source.Process();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ETW Listener exception: {ex}");
            }
            finally
            {
                _etwTraceEventSession = null;
            }
        }

        private void EtwWorkerProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (!(e.UserState is TraceEvent data))
                return;

            ProcessGenericEtwEvent(data);
        }

        /// <summary>
        /// Processes a generic ETW event and adds it to the EtwMessages collection.
        /// </summary>
        private void ProcessGenericEtwEvent(TraceEvent data)
        {
            try
            {
                string providerName = data.ProviderName ?? data.ProviderGuid.ToString();

                string eventName;
                try
                {
                    eventName = data.EventName ?? $"EventID_{(int)data.ID}";
                }
                catch (FormatException)
                {
                    eventName = $"EventID_{(int)data.ID}";
                }

                string formattedMessage = null;
                try
                {
                    formattedMessage = data.FormattedMessage;
                }
                catch (FormatException)
                {
                }

                var etwMessage = new EtwMessage(
                    (int)data.ID,
                    eventName,
                    providerName,
                    data.ProviderGuid)
                {
                    Timestamp = data.TimeStamp,
                    FormattedMessage = formattedMessage
                };

                // Extract payload fields
                try
                {
                    for (int i = 0; i < data.PayloadNames.Length; i++)
                    {
                        string name = data.PayloadNames[i];
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
                            etwMessage.PayloadFields[name] = valStr;
                        }
                    }
                }
                catch (FormatException)
                {
                }

                EtwMessages.Add(etwMessage);

                // Auto-scroll if enabled
                if (menuItemAutoScroll.IsChecked && ListBoxEtwMessages.Items.Count > 0)
                {
                    ListBoxEtwMessages.ScrollIntoView(ListBoxEtwMessages.Items[ListBoxEtwMessages.Items.Count - 1]);
                }

                // Update stream view if ETW tab is selected
                if (TabItemEtw.IsSelected)
                {
                    AppendEtwMessageToStream(etwMessage);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing generic ETW event: {ex.Message}");
            }
        }

        /// <summary>
        /// Appends a generic ETW message to the stream text editor.
        /// </summary>
        private void AppendEtwMessageToStream(EtwMessage message)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"<!-- [{message.Timestamp:HH:mm:ss.fff}] {message.ProviderName} - {message.EventName} (ID: {message.EventId}) -->");

            if (message.PayloadFields.Count > 0)
            {
                foreach (var kvp in message.PayloadFields)
                {
                    sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
                }
            }
            else if (!string.IsNullOrEmpty(message.FormattedMessage))
            {
                sb.AppendLine($"  Message: {message.FormattedMessage}");
            }

            sb.AppendLine();

            TextEditorEtwStream.AppendText(sb.ToString());

            if (menuItemAutoScroll.IsChecked)
            {
                TextEditorEtwStream.ScrollToEnd();
            }
        }

        /// <summary>
        /// Parses the provider text to extract GUIDs.
        /// </summary>
        private List<Guid> ParseProviderGuids(string text)
        {
            var guids = new List<Guid>();
            if (string.IsNullOrWhiteSpace(text))
                return guids;

            foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                    continue;

                trimmed = trimmed.Trim('{', '}', ' ');
                if (Guid.TryParse(trimmed, out Guid guid))
                {
                    guids.Add(guid);
                }
            }

            return guids;
        }

        /// <summary>
        /// Cleans up the ETW session on window close.
        /// </summary>
        internal void CleanupEtwSession()
        {
            StopEtwSession();
        }

        #region UI Event Handlers - Generic ETW

        private void ListBoxEtwMessages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListBoxEtwMessages.SelectedItem is EtwMessage etwMessage)
            {
                TextEditorEtwDetails.Text = etwMessage.GetFormattedPayload();
            }
        }

        private void ButtonClearEtw_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Do you really want to clear all ETW messages?", "Clear ETW Messages", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                EtwMessages.Clear();
                TextEditorEtwStream.Clear();
                TextEditorEtwDetails.Clear();
            }
        }

        private void ButtonSaveEtw_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Text files|*.txt|All files|*.*",
                FilterIndex = 0,
                DefaultExt = "txt",
                AddExtension = true,
                CheckPathExists = true,
                RestoreDirectory = true,
                Title = "Save ETW Stream",
                FileName = $"ETW-Stream-{Environment.MachineName}-{DateTime.Now:MM-dd-yy_H-mm-ss}.txt"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                System.IO.File.WriteAllText(saveFileDialog.FileName, TextEditorEtwStream.Text);
            }
        }

        private void CheckBoxEtwCapture_Changed(object sender, RoutedEventArgs e)
        {
            _etwTraceEnabled = CheckBoxEtwCapture.IsChecked == true;

            if (_etwTraceEnabled)
            {
                ImageEtwCapture.Visibility = Visibility.Visible;
                StartEtwSession();
            }
            else
            {
                ImageEtwCapture.Visibility = Visibility.Hidden;
                StopEtwSession();
            }
        }

        private void LabelBackToTopEtw_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            TextEditorEtwStream.ScrollToHome();
        }

        private void ButtonEtwOptions_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new EtwProviderDialog(
                _etwProviderText,
                _etwUseDynamicAll,
                _etwUseUnhandledEvents)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                _etwProviderText = dialog.ProviderText;
                _etwCustomProviders = dialog.ProviderGuids;
                _etwUseDynamicAll = dialog.UseDynamicAll;
                _etwUseUnhandledEvents = dialog.UseUnhandledEvents;

                // Update the label to show provider count
                LabelEtwProviderCount.Content = _etwCustomProviders.Count > 0
                    ? $"({_etwCustomProviders.Count} provider{(_etwCustomProviders.Count != 1 ? "s" : "")} configured)"
                    : "(no providers configured)";

                // Restart session with new providers if capture is active
                if (_etwTraceEnabled)
                {
                    RestartEtwSession();
                }
            }
        }

        #endregion
    }
}
