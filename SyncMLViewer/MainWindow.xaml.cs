// migrate to .NET 8 for nullable reference types or use .NET Core 3.0+
// https://devblogs.microsoft.com/dotnet/embracing-nullable-reference-types/
// #nullable enable

using ICSharpCode.AvalonEdit.Folding;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using System.Xml.XPath;
using SyncMLViewer.Properties;
using Path = System.IO.Path;

namespace SyncMLViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        // Thanks to Matt Graeber - @mattifestation - for the extended ETW Provider list
        // https://gist.github.com/mattifestation/04e8299d8bc97ef825affe733310f7bd/
        // https://gist.githubusercontent.com/mattifestation/04e8299d8bc97ef825affe733310f7bd/raw/857bfbb31d0e12a8ebc48a95f95d298222bae1f6/NiftyETWProviders.json
        // ProviderName: Microsoft.Windows.DeviceManagement.OmaDmClient
        private static readonly Guid OmaDmClient = new Guid("{0EC685CD-64E4-4375-92AD-4086B6AF5F1D}");

        // more MDM ETW Provider details
        // https://docs.microsoft.com/en-us/windows/client-management/mdm/diagnose-mdm-failures-in-windows-10
        // 3b9602ff-e09b-4c6c-bc19-1a3dfa8f2250	= Microsoft-WindowsPhone-OmaDm-Client-Provider
        // 3da494e4-0fe2-415C-b895-fb5265c5c83b = Microsoft-WindowsPhone-Enterprise-Diagnostics-Provider
        private static readonly Guid OmaDmClientProvider = new Guid("{3B9602FF-E09B-4C6C-BC19-1A3DFA8F2250}");

        // interestingly Microsoft-WindowsPhone-Enterprise-Diagnostics-Provider is not needed...
        //private static readonly Guid EnterpriseDiagnosticsProvider = new Guid("{3da494e4-0fe2-415C-b895-fb5265c5c83b}");

        private const string UpdateXmlUri =
            "https://github.com/okieselbach/SyncMLViewer/raw/master/SyncMLViewer/dist/update.xml";
        private const string Update2XmlUri =
            "https://github.com/okieselbach/SyncMLViewer/raw/master/SyncMLViewer/dist/update2.xml";

        private const string SessionName = "SyncMLViewer";
        private readonly BackgroundWorker _backgroundWorker;
        private readonly Runspace _rs;
        private readonly FoldingManager _foldingManager;
        private readonly XmlFoldingStrategy _foldingStrategy;
        private readonly string _version;
        private string _updateTempFileName;
        private bool _updateStarted;
        private bool _updateCheckInitial;

        private readonly MdmDiagnostics _mdmDiagnostics = new MdmDiagnostics();

        private SyncMlProgress SyncMlProgress { get; }
        private ObservableCollection<SyncMlSession> SyncMlSessions { get; }
        private ObservableCollection<SyncMlMessage> SyncMlMlMessages { get; set; }

        public MainWindow()
        {
            InitializeComponent();

            LabelStatus.Visibility = Visibility.Hidden;
            LabelStatusTop.Visibility = Visibility.Hidden;
            ButtonRestartUpdate.Visibility = Visibility.Hidden;
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            _version = $"{version.Major}.{version.Minor}.{version.Build}";
            this.Title += $" - {_version}";

            SyncMlProgress = new SyncMlProgress();
            SyncMlSessions = new ObservableCollection<SyncMlSession>();
            SyncMlMlMessages = new ObservableCollection<SyncMlMessage>();

            _rs = RunspaceFactory.CreateRunspace();
            _rs.Open();

            _backgroundWorker = new BackgroundWorker
            {
                WorkerReportsProgress = true
            };
            _backgroundWorker.DoWork += WorkerTraceEvents;
            _backgroundWorker.ProgressChanged += WorkerProgressChanged;
            _backgroundWorker.RunWorkerAsync();

            DataContext = this;

            this.Loaded += delegate { MenuItemCheckUpdate_OnClick(null, new RoutedEventArgs()); };

            ListBoxSessions.ItemsSource = SyncMlSessions;
            ListBoxSessions.DisplayMemberPath = "Entry";

            ListBoxMessages.ItemsSource = SyncMlMlMessages;
            ListBoxMessages.DisplayMemberPath = "Entry";

            ICSharpCode.AvalonEdit.Search.SearchPanel.Install(TextEditorStream);
            ICSharpCode.AvalonEdit.Search.SearchPanel.Install(TextEditorMessages);
            ICSharpCode.AvalonEdit.Search.SearchPanel.Install(TextEditorCodes);
            ICSharpCode.AvalonEdit.Search.SearchPanel.Install(TextEditorDiagnostics);
            _foldingManager = FoldingManager.Install(TextEditorMessages.TextArea);
            _foldingStrategy = new XmlFoldingStrategy();
            _foldingStrategy.UpdateFoldings(_foldingManager, TextEditorMessages.Document);

            LabelDeviceName.Content = Environment.MachineName;
            _updateStarted = false;
            _updateCheckInitial = true;

            TextEditorStream.Options.HighlightCurrentLine = true;
            TextEditorMessages.Options.HighlightCurrentLine = true;

            TextEditorCodes.Options.EnableHyperlinks = true;
            TextEditorCodes.Options.RequireControlModifierForHyperlinkClick = false;
            TextEditorCodes.Text = Properties.Resources.StatusCodes;

            TextEditorAbout.Options.EnableHyperlinks = true;
            TextEditorAbout.Options.RequireControlModifierForHyperlinkClick = false;
            TextEditorAbout.Text = Properties.Resources.About;

            TextEditorDiagnostics.Text +=
                $"Hostname:           {MdmDiagnostics.Hostname}\r\n" +
                $"OS Version:         {MdmDiagnostics.OsVersion} (x{MdmDiagnostics.Bits})\r\n" +
                $"Display Version:    {MdmDiagnostics.DisplayVersion}\r\n" +  
                $"Version:            {MdmDiagnostics.Version}\r\n" +
                $"Current Build:      {MdmDiagnostics.CurrentBuild}.{MdmDiagnostics.BuildRevision}\r\n" +
                $"Release ID:         {MdmDiagnostics.ReleaseId}\r\n" +
                $"Build Branch:       {MdmDiagnostics.BuildBranch}\r\n" +
                $"Enrollment UPN:     {_mdmDiagnostics.Upn}\r\n" +
                $"AAD TenantID:       {_mdmDiagnostics.AadTenantId}\r\n" +
                $"OMA-DM AccountID:   {_mdmDiagnostics.OmaDmAccountId}";
        }

        private static void WorkerTraceEvents(object sender, DoWorkEventArgs e)
        {
            try
            {
                if (TraceEventSession.IsElevated() != true)
                    throw new InvalidOperationException(
                        "Collecting ETW trace events requires administrative privileges.");

                if (TraceEventSession.GetActiveSessionNames().Contains(SessionName))
                {
                    Debug.WriteLine(
                        $"The session name '{SessionName}' is already in use, stopping existing and restart a new one.");
                    TraceEventSession.GetActiveSession(SessionName).Stop(true);
                }

                // An End-To-End ETW Tracing Example: EventSource and TraceEvent
                // https://blogs.msdn.microsoft.com/vancem/2012/12/20/an-end-to-end-etw-tracing-example-eventsource-and-traceevent/
                using (var traceEventSession = new TraceEventSession(SessionName))
                {
                    traceEventSession.StopOnDispose = true;
                    using (var traceEventSource = new ETWTraceEventSource(SessionName, TraceEventSourceType.Session))
                    {
                        traceEventSession.EnableProvider(OmaDmClient);
                        traceEventSession.EnableProvider(OmaDmClientProvider);

                        new RegisteredTraceEventParser(traceEventSource).All += (data =>
                            (sender as BackgroundWorker)?.ReportProgress(0, data.Clone()));
                        traceEventSource.Process();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception: {ex}");
            }
        }

        private void WorkerProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            try
            {
                if (!(e.UserState is TraceEvent userState))
                    throw new ArgumentException("No TraceEvent received.");

                // show all events
                if (CheckBoxShowTraceEvents.IsChecked == true)
                {
                    // filter a bit otherwise too much noise...
                    if (!string.Equals(userState.EventName, "FunctionEntry",
                            StringComparison.CurrentCultureIgnoreCase) &&
                        !string.Equals(userState.EventName, "FunctionExit",
                            StringComparison.CurrentCultureIgnoreCase) &&
                        !string.Equals(userState.EventName, "GenericLogEvent",
                            StringComparison.CurrentCultureIgnoreCase))
                    {
                        var message = userState.EventName + " ";
                        TextEditorStream.AppendText(message);
                        if (menuItemAutoScroll.IsChecked)
                        {
                            TextEditorStream.ScrollToEnd();
                        }

                        if (menuItemBackgroundLogging.IsChecked)
                        {
                            Trace.WriteLine(message);
                        }
                    }
                }

                // we are interested in just a few events with relevant data
                if (string.Equals(userState.EventName, "OmaDmClientExeStart",
                        StringComparison.CurrentCultureIgnoreCase) ||
                    string.Equals(userState.EventName, "OmaDmSyncmlVerboseTrace",
                        StringComparison.CurrentCultureIgnoreCase))
                {
                    SyncMlProgress.NotInProgress = false;
                    LabelStatus.Content = "Sync is in progress";
                    LabelStatus.Visibility = Visibility.Visible;

                    string eventDataText = null;
                    try
                    {
                        eventDataText = Encoding.UTF8.GetString(userState.EventData());
                    }
                    catch (Exception)
                    {
                        // ignored
                    }

                    if (eventDataText == null) return;

                    var startIndex = eventDataText.IndexOf("<SyncML", StringComparison.CurrentCultureIgnoreCase);
                    if (startIndex == -1) return;

                    var valueSyncMl =
                        TryFormatXml(eventDataText.Substring(startIndex, eventDataText.Length - startIndex - 1));

                    var message = string.Empty;

                    if (TextEditorStream.Text.Length == 0)
                    {
                        if (menuItemTimestamps.IsChecked)
                        {
                            message = $"<!-- {DateTime.Now} -->" + Environment.NewLine + valueSyncMl + Environment.NewLine;
                        }
                        else
                        {
                            message = valueSyncMl + Environment.NewLine;
                        }
                    }
                    else
                    {
                        if (menuItemTimestamps.IsChecked)
                        {
                            message = Environment.NewLine + $"<!-- {DateTime.Now} -->" + Environment.NewLine + valueSyncMl + Environment.NewLine;
                        }
                        else
                        {
                            message = Environment.NewLine + valueSyncMl + Environment.NewLine;
                        }
                    }

                    if (menuItemBackgroundLogging.IsChecked)
                    {
                        Trace.WriteLine(message);
                    }
                    else
                    {
                        TextEditorStream.AppendText(message);
                        if (menuItemAutoScroll.IsChecked)
                        {
                            TextEditorStream.ScrollToEnd();
                        }
                    }

                    if (!menuItemBackgroundLogging.IsChecked)
                    {
                        TextEditorMessages.Text = valueSyncMl;
                        _foldingStrategy.UpdateFoldings(_foldingManager, TextEditorMessages.Document);

                        var valueSessionId = "0";
                        var matchSessionId = new Regex("<SessionID>([0-9a-zA-Z]+)</SessionID>").Match(valueSyncMl);
                        if (matchSessionId.Success)
                            valueSessionId = matchSessionId.Groups[1].Value;

                        if (!SyncMlSessions.Any(item => item.SessionId == valueSessionId))
                        {
                            var syncMlSession = new SyncMlSession(valueSessionId);
                            SyncMlSessions.Add(syncMlSession);
                            SyncMlMlMessages.Clear();
                        }

                        var valueMsgId = "0";
                        var matchMsgId = new Regex("<MsgID>([0-9]+)</MsgID>").Match(valueSyncMl);
                        if (matchMsgId.Success)
                            valueMsgId = matchMsgId.Groups[1].Value;

                        var syncMlMessage = new SyncMlMessage(valueSessionId, valueMsgId, valueSyncMl);
                        SyncMlSessions.FirstOrDefault(item => item.SessionId == valueSessionId)?.Messages
                            .Add(syncMlMessage);
                        SyncMlMlMessages.Add(syncMlMessage);
                    }
                }
                else if (string.Equals(userState.EventName, "OmaDmSessionStart",
                    StringComparison.CurrentCultureIgnoreCase))
                {
                    var message = "<!-- OmaDmSessionStart -->" + Environment.NewLine;
                    if (menuItemBackgroundLogging.IsChecked)
                    {
                        Trace.WriteLine(message);
                    }
                    else
                    {
                        TextEditorStream.AppendText(message);
                        if (menuItemAutoScroll.IsChecked)
                        {
                            TextEditorStream.ScrollToEnd();
                        }
                    }
                }
                else if (string.Equals(userState.EventName, "OmaDmSessionComplete",
                    StringComparison.CurrentCultureIgnoreCase))
                {
                    var message = Environment.NewLine + "<!-- OmaDmSessionComplete -->" + Environment.NewLine;
                    if (menuItemBackgroundLogging.IsChecked)
                    {
                        Trace.WriteLine(message);
                    }
                    else
                    {
                        TextEditorStream.AppendText(message);
                        if (menuItemAutoScroll.IsChecked)
                        {
                            TextEditorStream.ScrollToEnd();
                        }
                    }
                    SyncMlProgress.NotInProgress = true;
                    LabelStatus.Visibility = Visibility.Hidden;
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private static string TryFormatXml(string text)
        {
            try
            {
                // HtmlDecode did too much here... WebUtility.HtmlDecode(XElement.Parse(text).ToString());
                return XElement.Parse(text).ToString();
            }
            catch (Exception)
            {
                return text;
            }
        }

        private void ButtonSync_Click(object sender, RoutedEventArgs e)
        {
            // trigger MDM sync via scheduled task with PowerShell
            // https://oofhours.com/2019/09/28/forcing-an-mdm-sync-from-a-windows-10-client/

            //using (var ps = PowerShell.Create())
            //{
            //    ps.Runspace = _rs;
            //    ps.AddScript("Get-ScheduledTask | ? {$_.TaskName -eq 'PushLaunch'} | Start-ScheduledTask");
            //    ps.Invoke();
            //}

            // found to be not that reliable, alternative approach is rock solid as it is the direct API call!

            // Alternative approach via WindowsRuntime
            var script = "[Windows.Management.MdmSessionManager,Windows.Management,ContentType=WindowsRuntime]\n" +
                         "$session = [Windows.Management.MdmSessionManager]::TryCreateSession()\n" +
                         "$session.StartAsync()";

            using (var ps = PowerShell.Create())
            {
                ps.Runspace = _rs;
                ps.AddScript(script);
                ps.Invoke();
            }

            SyncMlProgress.NotInProgress = false;
            LabelStatus.Content = "Sync triggered";
            LabelStatus.Visibility = Visibility.Visible;
        }

        private void CheckBoxHtmlDecode_Checked(object sender, RoutedEventArgs e)
        {
            if (((CheckBox)sender).IsChecked == true)
            {
                TextEditorMessages.Text = TextEditorMessages.Text.Replace("&lt;", "<").Replace("&gt;", ">")
                    .Replace("&quot;", "\"");
            }
            else
            {
                TextEditorMessages.Text = ((SyncMlMessage)ListBoxMessages.SelectedItems[0]).Xml;
            }
        }

        private void ButtonClear_Click(object sender, RoutedEventArgs e)
        {
            SyncMlSessions.Clear();
            SyncMlMlMessages.Clear();

            TextEditorMessages.Clear();
            TextEditorStream.Clear();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            // Cleanup TraceSession, Listener and temp update files...

            TraceEventSession.GetActiveSession(SessionName)?.Stop(true);
            _backgroundWorker.Dispose();

            Trace.Close();
            Trace.Listeners.Remove("listenerSyncMLStream");

            if (_updateStarted) return;
            try
            {
                if (_updateTempFileName == null) return;
                if (!File.Exists(_updateTempFileName))
                    File.Delete(_updateTempFileName);
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private void ListBoxMessages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(ListBoxMessages.SelectedItem is SyncMlMessage selectedItem))
                return;
            TextEditorMessages.Text = selectedItem.Xml;
            _foldingStrategy.UpdateFoldings(_foldingManager, TextEditorMessages.Document);

            CheckBoxHtmlDecode.IsChecked = false;
        }

        private void ListBoxSessions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(ListBoxSessions.SelectedItem is SyncMlSession selectedItem))
                return;

            ListBoxMessages.ItemsSource = selectedItem.Messages;
            ListBoxMessages.Items.Refresh();

            if (ListBoxMessages.Items.Count > 0)
                ListBoxMessages.SelectedIndex = 0;

            SyncMlMlMessages = selectedItem.Messages;
        }

        private void MenuItemExit_OnClick(object sender, RoutedEventArgs e)
        {
            if (!_updateStarted)
            {
                try
                {
                    if (_updateTempFileName != null)
                    {
                        if (!File.Exists(_updateTempFileName))
                            File.Delete(_updateTempFileName);
                    }
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            Application.Current.Shutdown(0);
        }

        private void MenuItemAbout_Click(object sender, RoutedEventArgs e)
        {
            TabControlSyncMlViewer.SelectedItem = TabItemAbout;
        }

        private void MenuItemCodes_Click(object sender, RoutedEventArgs e)
        {
            TabControlSyncMlViewer.SelectedItem = TabItemCodes;
        }

        private void MenuItemDiagnostics_Click(object sender, RoutedEventArgs e)
        {
            TabControlSyncMlViewer.SelectedItem = TabItemDiagnostics;
        }

        private async void MenuItemCheckUpdate_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var webClient = new WebClient())
                {
                    var systemWebProxy = WebRequest.GetSystemWebProxy();
                    systemWebProxy.Credentials = CredentialCache.DefaultCredentials;
                    webClient.Proxy = systemWebProxy;

                    var updateUrl = UpdateXmlUri;
                    if (Settings.Default.Properties["DeveloperPreview"] != null)
                    {
                        if (Settings.Default.DeveloperPreview)
                            updateUrl = Update2XmlUri;
                    }

                    var data = await webClient.DownloadDataTaskAsync(new Uri(updateUrl));
                    var xDocument = XDocument.Load(new MemoryStream(data));

                    var url = xDocument.XPathSelectElement("./LatestVersion/DownloadURL")?.Value;
                    var version = xDocument.XPathSelectElement("./LatestVersion/VersionNumber")?.Value;

                    if (url == null || !url.StartsWith("https")) return;
                    if (version == null) return;
                    if (string.CompareOrdinal(version, 0, _version, 0, version.Length) <= 0) return;

                    if (_updateCheckInitial)
                    {
                        LabelUpdateIndicator.Content =
                            LabelUpdateIndicator.Content.ToString().Replace("[0.0.0]", version);
                        LabelUpdateIndicator.Visibility = Visibility.Visible;
                        _updateCheckInitial = false;
                        return;
                    }

                    LabelUpdateIndicator.Visibility = Visibility.Hidden;
                    ButtonRestartUpdate.Content =
                        ButtonRestartUpdate.Content.ToString().Replace("[0.0.0]", version);

                    _updateTempFileName = Path.Combine(Path.GetTempPath(), $"{Path.GetRandomFileName()}.zip");
                    if (_updateTempFileName == null) return;

                    await webClient.DownloadFileTaskAsync(new Uri(url), _updateTempFileName);

                    if (!File.Exists(_updateTempFileName)) return;

                    // simple sanity check, bigger than 10KB? we assume it is not a dummy or broken binary (e.g. 0 KB file)
                    if (new FileInfo(_updateTempFileName).Length > 1024 * 10)
                        ButtonRestartUpdate.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private void ButtonRestartUpdate_Click(object sender, RoutedEventArgs e)
        {
            var path = Assembly.GetExecutingAssembly().Location;

            try
            {
                // call a separate process (PowerShell) to extract and overwrite the app binaries after app shutdown...
                // to give the app enough time for shutdown we wait 2 seconds before replacing - prevent currently in use scenarios
                using (var p = new Process())
                {
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.FileName = "PowerShell.exe";
                    p.StartInfo.Arguments = "-ex bypass -command &{ Start-Sleep 2; Expand-Archive -Path \"" +
                                            _updateTempFileName + "\" -DestinationPath \"" +
                                            Path.GetDirectoryName(path) +
                                            "\" -Force; Remove-Item -Path \"" + _updateTempFileName +
                                            "\" -Force; Start-Process \"" + path + "\"}";
                    p.StartInfo.CreateNoWindow = true;
                    p.Start();

                    _updateStarted = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                try
                {
                    if (_updateTempFileName != null)
                    {
                        if (!File.Exists(_updateTempFileName))
                            File.Delete(_updateTempFileName);
                    }
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            Application.Current.Shutdown(0);
        }

        private void MenuItemRegistryPolicyManager_OnClick(object sender, RoutedEventArgs e)
        {
            Helper.OpenRegistry(@"Computer\HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager");
        }

        private void MenuItemRegistryProvisioning_Click(object sender, RoutedEventArgs e)
        {
            Helper.OpenRegistry(@"Computer\HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Provisioning");
        }

        private async void MenuItemMdmDiagnostics_OnClick(object sender, RoutedEventArgs e)
        {
            LabelStatusTop.Visibility = Visibility.Visible;

            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "MDMDiagnostics", "MdmDiagnosticsTool");
            Directory.CreateDirectory(path);
            await Task.Factory.StartNew(() =>
            {
                var p = new Process
                {
                    StartInfo =
                    {
                        UseShellExecute = false,
                        FileName = "MdmDiagnosticsTool.exe",
                        Arguments = $"-out {path}",
                        CreateNoWindow = true
                    }
                };

                p.Start();
                p.WaitForExit();
                Debug.WriteLine($"MdmDiagnosticsTool ExitCode: {p.ExitCode}");
            });
            var exp = new Process
            {
                StartInfo =
                {
                    FileName = "explorer.exe",
                    Arguments = path
                }
            };
            exp.Start();
            exp.Dispose();

            LabelStatusTop.Visibility = Visibility.Hidden;
        }

        private async void MenuItemMdmDiagnosticsAutopilot_OnClick(object sender, RoutedEventArgs e)
        {
            LabelStatusTop.Visibility = Visibility.Visible;

            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "MDMDiagnostics", "MdmDiagnosticsTool");
            Directory.CreateDirectory(path);
            await Task.Factory.StartNew(() =>
            {
                var p = new Process
                {
                    StartInfo =
                    {
                        UseShellExecute = false,
                        FileName = "MdmDiagnosticsTool.exe",
                        Arguments = $"-area Autopilot -zip {Path.Combine(path, "Autopilot.zip")}",
                        CreateNoWindow = true
                    }
                };

                p.Start();
                p.WaitForExit();
                Debug.WriteLine($"MdmDiagnosticsTool ExitCode: {p.ExitCode}");
            });
            var exp = new Process
            {
                StartInfo =
                {
                    FileName = "explorer.exe",
                    Arguments = path
                }
            };
            exp.Start();
            exp.Dispose();

            LabelStatusTop.Visibility = Visibility.Hidden;
        }

        private async void MenuItemMdmDiagnosticsDeviceEnrollment_OnClick(object sender, RoutedEventArgs e)
        {
            LabelStatusTop.Visibility = Visibility.Visible;

            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "MDMDiagnostics", "MdmDiagnosticsTool");
            Directory.CreateDirectory(path);
            await Task.Factory.StartNew(() =>
            {
                var p = new Process
                {
                    StartInfo =
                    {
                        UseShellExecute = false,
                        FileName = "MdmDiagnosticsTool.exe",
                        Arguments = $"-area DeviceEnrollment -zip {Path.Combine(path, "DeviceEnrollment.zip")}",
                        CreateNoWindow = true
                    }
                };

                p.Start();
                p.WaitForExit();
                Debug.WriteLine($"MdmDiagnosticsTool ExitCode: {p.ExitCode}");
            });
            var exp = new Process
            {
                StartInfo =
                {
                    FileName = "explorer.exe",
                    Arguments = path
                }
            };
            exp.Start();
            exp.Dispose();

            LabelStatusTop.Visibility = Visibility.Hidden;
        }

        private async void MenuItemMdmDiagnosticsDeviceProvisioning_OnClick(object sender, RoutedEventArgs e)
        {
            LabelStatusTop.Visibility = Visibility.Visible;

            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "MDMDiagnostics", "MdmDiagnosticsTool");
            Directory.CreateDirectory(path);
            await Task.Factory.StartNew(() =>
            {
                var p = new Process
                {
                    StartInfo =
                    {
                        UseShellExecute = false,
                        FileName = "MdmDiagnosticsTool.exe",
                        Arguments = $"-area DeviceProvisioning -zip {Path.Combine(path, "DeviceProvisioning.zip")}",
                        CreateNoWindow = true
                    }
                };

                p.Start();
                p.WaitForExit();
                Debug.WriteLine($"MdmDiagnosticsTool ExitCode: {p.ExitCode}");
            });
            var exp = new Process
            {
                StartInfo =
                {
                    FileName = "explorer.exe",
                    Arguments = path
                }
            };
            exp.Start();
            exp.Dispose();

            LabelStatusTop.Visibility = Visibility.Hidden;
        }

        private async void MenuItemMdmDiagnosticsTpm_OnClick(object sender, RoutedEventArgs e)
        {
            LabelStatusTop.Visibility = Visibility.Visible;

            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "MDMDiagnostics", "MdmDiagnosticsTool");
            Directory.CreateDirectory(path);
            await Task.Factory.StartNew(() =>
            {
                var p = new Process
                {
                    StartInfo =
                    {
                        UseShellExecute = false,
                        FileName = "MdmDiagnosticsTool.exe",
                        Arguments = $"-area TPM -zip {Path.Combine(path, "TPM.zip")}",
                        CreateNoWindow = true
                    }
                };

                p.Start();
                p.WaitForExit();
                Debug.WriteLine($"MdmDiagnosticsTool ExitCode: {p.ExitCode}");
            });
            var exp = new Process
            {
                StartInfo =
                {
                    FileName = "explorer.exe",
                    Arguments = path
                }
            };
            exp.Start();
            exp.Dispose();

            LabelStatusTop.Visibility = Visibility.Hidden;
        }

        private void MenuItemBackgroundLogging_Checked(object sender, RoutedEventArgs e)
        {
            if (((MenuItem)sender).IsChecked)
            {
                Trace.Listeners.Add(new TextWriterTraceListener($"SyncMLStream-BackgroundLogging-{Environment.MachineName}-{DateTime.Now:MM-dd-yy_H-mm-ss}.xml", "listenerSyncMLStream"));
                Trace.AutoFlush = true;

                TextEditorStream.Clear();
                TextEditorMessages.Clear();

                SyncMlSessions.Clear();
                SyncMlMlMessages.Clear();

                TextEditorStream.IsEnabled = false;
                TextEditorStream.AppendText(Environment.NewLine + "\t'Background Logging Mode' enabled.");
            }
        }

        private void MenuItemBackgroundLogging_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!((MenuItem)sender).IsChecked)
            {
                Trace.Close();
                Trace.Listeners.Remove("listenerSyncMLStream");

                TextEditorStream.Clear();
                TextEditorStream.IsEnabled = true;
            }
        }

        private void MenuItemOpen_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                InitialDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                Filter = "Xml files|*.xml|All files|*.*",
                FilterIndex = 0,
                RestoreDirectory = true,
                Title = "Open SyncML stream",
            };

            if (openFileDialog.ShowDialog() ==  true)
            {
                TextEditorStream.Clear();
                TextEditorMessages.Clear();

                SyncMlSessions.Clear();
                SyncMlMlMessages.Clear();

                var fileStream = openFileDialog.OpenFile();
                using (StreamReader reader = new StreamReader(fileStream))
                {
                    TextEditorStream.Text = reader.ReadToEnd();
                }

                if (TextEditorStream.Text.Length > 0)
                {
                    var syncMlMessages = Regex.Matches(TextEditorStream.Text, @"<SyncML[\s\S]*?</SyncML>", RegexOptions.IgnoreCase);
                    foreach (Match message in syncMlMessages)
                    {
                        var valueSyncMl = TryFormatXml(message.Value);

                        TextEditorMessages.Text = valueSyncMl;
                        _foldingStrategy.UpdateFoldings(_foldingManager, TextEditorMessages.Document);

                        var valueSessionId = "0";
                        var matchSessionId = new Regex("<SessionID>([0-9a-zA-Z]+)</SessionID>", RegexOptions.IgnoreCase).Match(valueSyncMl);
                        if (matchSessionId.Success)
                            valueSessionId = matchSessionId.Groups[1].Value;

                        if (!SyncMlSessions.Any(item => item.SessionId == valueSessionId))
                        {
                            var syncMlSession = new SyncMlSession(valueSessionId);
                            SyncMlSessions.Add(syncMlSession);
                            SyncMlMlMessages.Clear();
                        }

                        var valueMsgId = "0";
                        var matchMsgId = new Regex("<MsgID>([0-9]+)</MsgID>", RegexOptions.IgnoreCase).Match(valueSyncMl);
                        if (matchMsgId.Success)
                            valueMsgId = matchMsgId.Groups[1].Value;

                        var syncMlMessage = new SyncMlMessage(valueSessionId, valueMsgId, valueSyncMl);
                        SyncMlSessions.FirstOrDefault(item => item.SessionId == valueSessionId)?.Messages
                            .Add(syncMlMessage);
                        SyncMlMlMessages.Add(syncMlMessage);
                    }
                }
            }
        }

        private void ButtonSaveAs_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog fileDialog = new SaveFileDialog
            {
                Filter = "Xml files|*.xml|All files|*.*",
                FilterIndex = 0,
                DefaultExt = "xml",
                AddExtension = true,
                CheckPathExists = true,
                RestoreDirectory = true,
                Title = "Save SyncML stream",
                FileName = $"SyncMLStream-{Environment.MachineName}-{DateTime.Now:MM-dd-yy_H-mm-ss}.xml"
            };

            fileDialog.FileOk += (o, args) => File.WriteAllText(((FileDialog)o).FileName, TextEditorStream.Text);
            fileDialog.ShowDialog();
        }

        private void MenuItemAlwaysOnTop_Checked(object sender, RoutedEventArgs e)
        {
            this.Topmost = true;
        }

        private void MenuItemAlwaysOnTop_Unchecked(object sender, RoutedEventArgs e)
        {
            this.Topmost = false;
        }
    }
}

