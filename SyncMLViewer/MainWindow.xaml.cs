// migrate to .NET 8 for nullable reference types or use .NET Core 3.0+
// https://devblogs.microsoft.com/dotnet/embracing-nullable-reference-types/
// #nullable enable

using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using SyncMLViewer.Properties;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Net.Cache;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Application = System.Windows.Application;
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

        private const string UpdateXmlUri = "https://github.com/okieselbach/SyncMLViewer/raw/master/SyncMLViewer/dist/update.xml";
        private const string Update2XmlUri = "https://github.com/okieselbach/SyncMLViewer/raw/master/SyncMLViewer/dist/update2.xml";

        private const string SessionName = "SyncMLViewer";
        private readonly BackgroundWorker _backgroundWorker;
        private readonly Runspace _rs;
        private readonly FoldingManager _foldingManager;
        private readonly XmlFoldingStrategy _foldingStrategy;
        private readonly string _version;
        private string _updateTempFileName;
        private bool _updateStarted;
        private bool _updateCheckInitial;
        private bool _syncMDMSwitch;
        private bool _syncMMPCSwitch;
        private bool _backgroundLoggingSwitch;
        private bool _hideWhenMinimizedSwitch;
        private System.Windows.Forms.NotifyIcon _notifyIcon;
        private bool _notifyIconBallonShownOnce;
        private WindowState _storedWindowState = WindowState.Normal;
        private readonly MdmDiagnostics _mdmDiagnostics = new MdmDiagnostics();
        private int _CmdIdCounter;
        private AutoCompleteModel _autoCompleteModel = new AutoCompleteModel();
        private StatusCodeLookupModel _statusCodeLookupModel = new StatusCodeLookupModel();

        public List<WifiProfile> WifiProfileList { get; set; }
        public List<VpnProfile> VpnProfileList { get; set; }

        static public TraceEventSessionState TraceEventSessionState { get; set; }
        public SyncMlProgress SyncMlProgress { get; set; }
        public ObservableCollection<SyncMlSession> SyncMlSessions { get; }

        public ICommand OpenCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand ExitCommand { get; }
        public ICommand DecodeBase64Command { get; }
        public ICommand DecodeCertCommand { get; }
        public ICommand DecodeHtmlCommand { get; }
        public ICommand WordWrapCommand { get; }
        public ICommand MdmSyncCommand { get; }
        public ICommand MmpcSyncCommand { get; }
        public ICommand RunRequestCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand HelpCspCommand { get; }
        public ICommand StatusCodeCommand { get; }
        public ICommand FormatCommand { get; }
        public ICommand TopMostCommand { get; }
        public ICommand AutoScrollCommand { get; }
        public ICommand ShowCharsCommand { get; }
        public ICommand BackgroundLogCommand { get; }
        public ICommand RegPolicyManagerCommand { get; }
        public ICommand RegEnrollmentCommand { get; }
        public ICommand RegProvisioningCommand { get; }
        public ICommand RegImeCommand { get; }
        public ICommand RegDcCommand { get; }
        public ICommand RegRebootUrisCommand { get; }
        public ICommand CheckUpdateCommand { get; }
        public ICommand HideMinimizedCommand { get; }
        public ICommand ResetSyncCommand { get; }
        public ICommand MdmEventLogCommand { get; }
        public ICommand MdmReportCommand { get; }
        public ICommand OpenImeLogsCommand { get; }
        public ICommand OpenMdmLogsCommand { get; }
        public ICommand OpenDcFolderCommand { get; }

        public MainWindow()
        {
            InitializeComponent();

            OpenCommand = new RelayCommand(() => { MenuItemOpen_Click(null, null); });
            SaveCommand = new RelayCommand(() => { ButtonSaveAs_Click(null, null); });
            ExitCommand = new RelayCommand(() => { MenuItemExit_OnClick(null, null); });
            DecodeBase64Command = new RelayCommand(() => { MenuItemDecodeBase64_Click(null, null); });
            DecodeCertCommand = new RelayCommand(() => { MenuItemDecodeCertificate_Click(null, null); });
            DecodeHtmlCommand = new RelayCommand(() => { MenuItemDecodeHTML_Click(null, null); });
            WordWrapCommand = new RelayCommand(() => {
                menuItemWordWrap.IsChecked = !menuItemWordWrap.IsChecked;
                MenuItemWordWrap_Click(null, null); 
            });
            MdmSyncCommand = new RelayCommand(() => { ButtonMDMSync_Click(null, null); });
            MmpcSyncCommand = new RelayCommand(() => { ButtonMMPCSync_Click(null, null); });
            RunRequestCommand = new RelayCommand(() => { ButtonRunRequest_Click(null, null); });
            ClearCommand = new RelayCommand(() => { ButtonClear_Click(null, null); });
            HelpCspCommand = new RelayCommand(() => { MenuItemOpenHelp_Click(null, null); });
            StatusCodeCommand = new RelayCommand(() => { MenuItemLookupStatusCode_Click(null, null); });
            FormatCommand = new RelayCommand(() => { LabelFormat_MouseUp(null, null); });
            TopMostCommand = new RelayCommand(() => {
                menuItemAlwaysOnTop.IsChecked = !menuItemAlwaysOnTop.IsChecked;
                MenuItemAlwaysOnTop_Click(null, null);
            });
            AutoScrollCommand = new RelayCommand(() => { menuItemAutoScroll.IsChecked = !menuItemAutoScroll.IsChecked; });
            ShowCharsCommand = new RelayCommand(() => {
                menuItemShowAllChars.IsChecked = !menuItemShowAllChars.IsChecked;
                MenuItemShowAllChars_Click(null, null);
            });
            BackgroundLogCommand = new RelayCommand(() => {
                menuItemBackgroundLogging.IsChecked = !menuItemBackgroundLogging.IsChecked;
                MenuItemBackgroundLogging_Click(null, null);
            });
            RegPolicyManagerCommand = new RelayCommand(() => { MenuItemRegistryPolicyManager_OnClick(null, null); });
            RegEnrollmentCommand = new RelayCommand(() => { MenuItemRegistryEnrollments_Click(null, null); });
            RegProvisioningCommand = new RelayCommand(() => { MenuItemRegistryProvisioning_Click(null, null); });
            RegImeCommand = new RelayCommand(() => { MenuItemRegistryIntuneManagementExtension_Click(null, null); });
            RegDcCommand = new RelayCommand(() => { MenuItemRegistryDeclaredConfiguration_Click(null, null); });
            RegRebootUrisCommand = new RelayCommand(() => { MenuItemRegistryRebootRequiredUris_Click(null, null); });
            CheckUpdateCommand = new RelayCommand(() => { MenuItemCheckUpdate_OnClick(null, null); });
            HideMinimizedCommand = new RelayCommand(() => { menuItemHideWhenMinimized.IsChecked = !menuItemHideWhenMinimized.IsChecked; });
            ResetSyncCommand = new RelayCommand(() => { MenuItemResetSyncTriggerStatus_Click(null, null); });
            MdmEventLogCommand = new RelayCommand(() => { MenuItemOpenMdmEventLog_Click(null, null); });
            MdmReportCommand = new RelayCommand(() => { MenuItemRunMdmAdvancedDiagnosticReport_Click(null, null); });
            OpenImeLogsCommand = new RelayCommand(() => { MenuItemOpenImeLogs_Click(null, null); });
            OpenMdmLogsCommand = new RelayCommand(() => { MenuItemOpenMDMDiagnosticsFolder_Click(null, null); });
            OpenDcFolderCommand = new RelayCommand(() => { MenuItemOpenDeclaredConfigurationHostOSFolder_Click(null, null); });

            _syncMDMSwitch = false;
            _syncMMPCSwitch = false;
            _backgroundLoggingSwitch = false;
            _hideWhenMinimizedSwitch = false;
            _CmdIdCounter = 0;

            LabelStatus.Visibility = Visibility.Hidden;
            LabelStatusTop.Visibility = Visibility.Hidden;
            ButtonRestartUpdate.Visibility = Visibility.Hidden;
            LabelTruncatedDataIndicator.Visibility = Visibility.Hidden;
            LabelMessageStats.Content = "Message length: 0";
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            _version = $"{version.Major}.{version.Minor}.{version.Build}";
            Title += $" - {_version}";

            // based on this: https://possemeeg.wordpress.com/2007/09/06/minimize-to-tray-icon-in-wpf/
            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                BalloonTipText = "The app has been minimized. Click the tray icon to show.",
                BalloonTipTitle = "SyncML Viewer",
                Text = "SyncML Viewer"
            };
            Stream iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/;component/sync-icon.ico")).Stream;
            _notifyIcon.Icon = new System.Drawing.Icon(iconStream);
            _notifyIcon.Click += new EventHandler(NotifyIcon_Click);
            _notifyIconBallonShownOnce = false;

            TraceEventSessionState = new TraceEventSessionState();
            SyncMlProgress = new SyncMlProgress();
            SyncMlSessions = new ObservableCollection<SyncMlSession>();

            _rs = RunspaceFactory.CreateRunspace();
            _rs.Open();

            _backgroundWorker = new BackgroundWorker
            {
                WorkerReportsProgress = true
            };
            _backgroundWorker.DoWork += WorkerTraceEvents;
            _backgroundWorker.ProgressChanged += WorkerProgressChanged;
            _backgroundWorker.RunWorkerAsync();

            // a little hacky, setting DataContext (ViewModel) of the window to this class MainWindow
            DataContext = this;

            Loaded += delegate { MenuItemCheckUpdate_OnClick(null, new RoutedEventArgs()); };

            ListBoxSessions.ItemsSource = SyncMlSessions;
            ListBoxSessions.DisplayMemberPath = "Entry";

            ListBoxMessages.DisplayMemberPath = "Entry";

            WifiProfileList = new List<WifiProfile>();
            ListBoxWifi.ItemsSource = WifiProfileList;

            VpnProfileList = new List<VpnProfile>();
            ListBoxVpn.ItemsSource = VpnProfileList;

            ICSharpCode.AvalonEdit.Search.SearchPanel.Install(TextEditorStream);
            ICSharpCode.AvalonEdit.Search.SearchPanel.Install(TextEditorMessages);
            ICSharpCode.AvalonEdit.Search.SearchPanel.Install(TextEditorCodes);
            ICSharpCode.AvalonEdit.Search.SearchPanel.Install(TextEditorDiagnostics);
            ICSharpCode.AvalonEdit.Search.SearchPanel.Install(TextEditorSyncMlRequests);
            ICSharpCode.AvalonEdit.Search.SearchPanel.Install(TextEditorSyncMlRequestsRequestViewer);
            _foldingManager = FoldingManager.Install(TextEditorMessages.TextArea);
            _foldingStrategy = new XmlFoldingStrategy();
            _foldingStrategy.UpdateFoldings(_foldingManager, TextEditorMessages.Document);

            LabelProcessingTime.Content = string.Empty;
            LabelDeviceName.Content = Environment.MachineName;
            _updateStarted = false;
            _updateCheckInitial = true;

            TextEditorStream.Options.HighlightCurrentLine = true;
            TextEditorStream.Options.EnableRectangularSelection = true;
            TextEditorStream.WordWrap = false;

            TextEditorMessages.Options.HighlightCurrentLine = true;
            TextEditorMessages.Options.EnableRectangularSelection = true;
            TextEditorMessages.WordWrap = false;

            TextEditorSyncMlRequests.Options.HighlightCurrentLine = true;
            TextEditorSyncMlRequests.Options.EnableRectangularSelection = true;
            TextEditorSyncMlRequests.WordWrap = false;

            TextEditorSyncMlRequestsRequestViewer.Options.HighlightCurrentLine = false;
            TextEditorSyncMlRequestsRequestViewer.Options.EnableRectangularSelection = true;
            TextEditorSyncMlRequestsRequestViewer.Options.EnableTextDragDrop = true;
            TextEditorSyncMlRequestsRequestViewer.WordWrap = false;

            TextEditorCodes.Options.EnableHyperlinks = true;
            TextEditorCodes.Options.RequireControlModifierForHyperlinkClick = false;
            TextEditorCodes.Text = Properties.Resources.StatusCodes;

            TextEditorAbout.Options.EnableHyperlinks = true;
            TextEditorAbout.Options.RequireControlModifierForHyperlinkClick = false;
            TextEditorAbout.Text = Properties.Resources.About;

            TextEditorDiagnostics.Text +=
                $"Hostname:                 {MdmDiagnostics.Hostname}\r\n" +
                $"OS Version:               {MdmDiagnostics.OsVersion} (x{MdmDiagnostics.Bits})\r\n" +
                $"Display Version:          {MdmDiagnostics.DisplayVersion}\r\n" +
                $"Version:                  {MdmDiagnostics.Version}\r\n" +
                $"Current Build:            {MdmDiagnostics.CurrentBuild}.{MdmDiagnostics.BuildRevision}\r\n" +
                //$"Release ID:               {MdmDiagnostics.ReleaseId}\r\n" +
                $"Build Branch:             {MdmDiagnostics.BuildBranch}\r\n" +
                $"IME Version:              {MdmDiagnostics.IntuneAgentVersion}\r\n" +
                $"Logon Username:           {MdmDiagnostics.LogonUsername}\r\n" +
                $"Logon User SID:           {MdmDiagnostics.LogonUserSid}\r\n" +
                $"Enrollment UPN:           {_mdmDiagnostics.EnrollmentUpn}\r\n" +
                $"AAD TenantID:             {_mdmDiagnostics.AadTenantId}\r\n" +
                $"OMA-DM AccountID (MDM):   {_mdmDiagnostics.OmaDmAccountIdMDM}\r\n" +
                $"OMA-DM AccountID (MMP-C): {_mdmDiagnostics.OmaDmAccountIdMMPC}";
                
            // no MMP-C enrollment, disable button
            if (string.IsNullOrEmpty(_mdmDiagnostics.OmaDmAccountIdMMPC))
            {
                ButtonMMPCSync.IsEnabled = false;
            }

            // don't allow to open IME folder if IME is not installed
            try
            {
                var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\IntuneManagementExtension");
                if (key == null)
                {
                    menuItemIntuneManagementExtension.IsEnabled = false;
                }
            }
            catch (Exception)
            {
                // exceptions ignored
                menuItemIntuneManagementExtension.IsEnabled = false;
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (menuItemCleanupAfterExit.IsChecked)
            {
                try
                {
                    var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    List<string> files = new List<string>
                    {
                        { Path.Combine(assemblyLocation, Properties.Resources.Executer) },
                        { Path.Combine(assemblyLocation, Properties.Resources.OutputFile) },
                        { Path.Combine(assemblyLocation, Properties.Resources.InputFile) }
                    };

                    foreach (var item in files)
                    {
                        if (File.Exists(item))
                            File.Delete(item);
                    }
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        private void NotifyIcon_Click(object sender, EventArgs e)
        {
            Show();
            WindowState = _storedWindowState;
        }

        private void CheckTrayIcon()
        {
            ShowTrayIcon(!IsVisible);
        }

        private void ShowTrayIcon(bool show)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = show;
            }
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (menuItemHideWhenMinimized.IsChecked)
            {
                if (WindowState == WindowState.Minimized)
                {
                    Hide();
                    if (_notifyIcon != null && _notifyIconBallonShownOnce == false)
                    {
                        _notifyIcon.ShowBalloonTip(2000);
                        _notifyIconBallonShownOnce = true;
                    }
                }
                else
                {
                    _storedWindowState = WindowState;
                }
            }
        }

        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (menuItemHideWhenMinimized.IsChecked)
            {
                CheckTrayIcon();
            }
        }

        private void WorkerTraceEvents(object sender, DoWorkEventArgs e)
        {
            try
            {
                if (TraceEventSession.IsElevated() != true)
                {
                    throw new InvalidOperationException("Collecting ETW trace events requires administrative privileges.");
                }

                if (TraceEventSession.GetActiveSessionNames().Contains(SessionName))
                {
                    Debug.WriteLine($"The session name '{SessionName}' is already in use, stopping existing and restart a new one.");
                    TraceEventSession.GetActiveSession(SessionName).Stop(true);
                }

                // An End-To-End ETW Tracing Example: EventSource and TraceEvent
                // https://blogs.msdn.microsoft.com/vancem/2012/12/20/an-end-to-end-etw-tracing-example-eventsource-and-traceevent/
                using (var traceEventSession = new TraceEventSession(SessionName))
                {
                    traceEventSession.StopOnDispose = true;
                    using (var traceEventSource = new ETWTraceEventSource(SessionName, TraceEventSourceType.Session))
                    {
                        // https://docs.microsoft.com/en-us/windows/win32/api/evntrace/ns-evntrace-event_trace_properties
                        // !!! Regardless of buffer size, ETW cannot collect events larger than 64KB.

                        // => This results in truncated policies... :-( unaware how to deal with this to get the full event data then...

                        traceEventSession.EnableProvider(OmaDmClient);
                        traceEventSession.EnableProvider(OmaDmClientProvider);

                        new RegisteredTraceEventParser(traceEventSource).All += (data => (sender as BackgroundWorker)?.ReportProgress(0, data.Clone()));
                        traceEventSource.Process();

                        TraceEventSessionState.Started = true;
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
                {
                    throw new ArgumentException("No TraceEvent received.");
                }

                // show all events
                if (menuItemTraceEvents.IsChecked == true)
                {
                    // filter a bit otherwise too much noise...
                    if (!string.Equals(userState.EventName, "FunctionEntry", StringComparison.CurrentCultureIgnoreCase) &&
                        !string.Equals(userState.EventName, "FunctionExit", StringComparison.CurrentCultureIgnoreCase) &&
                        !string.Equals(userState.EventName, "GenericLogEvent", StringComparison.CurrentCultureIgnoreCase))
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
                if (string.Equals(userState.EventName, "OmaDmClientExeStart", StringComparison.CurrentCultureIgnoreCase) ||
                    string.Equals(userState.EventName, "OmaDmSyncmlVerboseTrace", StringComparison.CurrentCultureIgnoreCase))
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

                    var valueSyncMl = TryFormatXml(eventDataText.Substring(startIndex, eventDataText.Length - startIndex - 1));

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
                        try
                        {
                            _foldingStrategy.UpdateFoldings(_foldingManager, TextEditorMessages.Document);
                        }
                        catch (Exception)
                        {
                            // ignored
                        }

                        var valueSessionId = "0";
                        var matchSessionId = new Regex("<SessionID>([0-9a-zA-Z]+)</SessionID>").Match(valueSyncMl);
                        if (matchSessionId.Success)
                        {
                            valueSessionId = matchSessionId.Groups[1].Value;
                        }

                        if (!SyncMlSessions.Any(item => item.SessionId == valueSessionId))
                        {
                            var syncMlSession = new SyncMlSession(valueSessionId);
                            SyncMlSessions.Add(syncMlSession);
                        }

                        var valueMsgId = "0";
                        var matchMsgId = new Regex("<MsgID>([0-9]+)</MsgID>").Match(valueSyncMl);
                        if (matchMsgId.Success)
                        {
                            valueMsgId = matchMsgId.Groups[1].Value;
                        }

                        var syncMlMessage = new SyncMlMessage(valueSessionId, valueMsgId, valueSyncMl);
                        SyncMlSessions.FirstOrDefault(item => item.SessionId == valueSessionId)?.Messages.Add(syncMlMessage);
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

        private string TryFormatXml(string text)
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

        private void ButtonMDMSync_Click(object sender, RoutedEventArgs e)
        {
            if (menuItemAlternateMDMTrigger.IsChecked)
            {
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
            }
            else
            {
                // with linkedEnrollment I had the issue it triggerd all the time the MMPC sync,
                // so I switched to the scheduled task approach and trigger the correct task
                try
                {
                    var p = new Process
                    {
                        StartInfo = {
                                UseShellExecute = false,
                                FileName = "SCHTASKS.exe",
                                RedirectStandardError = true,
                                RedirectStandardOutput = true,
                                CreateNoWindow = true,
                                WindowStyle = ProcessWindowStyle.Hidden,
                                Arguments = $"/Run /I /TN \"Microsoft\\Windows\\EnterpriseMgmt\\{_mdmDiagnostics.OmaDmAccountIdMDM}\\Schedule #3 created by enrollment client\""
                            }
                    };
                    p.Start();

                    // alternative way is to trigger the DeviceEnroller.exe from the scheduled task directly,
                    // but when something changes I will miss maybe new args or other things..., staying with the task for the time being
                    // Process.Start(Environment.ExpandEnvironmentVariables(@"%windir%\system32\DeviceEnroller.exe"), $"/o \"{_mdmDiagnostics.OmaDmAccountIdMDM}\" /c /b");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("MDM Sync", "MDM Sync failed to start\n\n" + ex.ToString(), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            SyncMlProgress.NotInProgress = false;
            LabelStatus.Content = "Sync triggered";
            LabelStatus.Visibility = Visibility.Visible;
        }

        private void ButtonMMPCSync_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var p = new Process
                {
                    StartInfo = {
                                UseShellExecute = false,
                                FileName = "SCHTASKS.exe",
                                RedirectStandardError = true,
                                RedirectStandardOutput = true,
                                CreateNoWindow = true,
                                WindowStyle = ProcessWindowStyle.Hidden,
                                Arguments = $"/Run /I /TN \"Microsoft\\Windows\\EnterpriseMgmt\\{_mdmDiagnostics.OmaDmAccountIdMMPC}\\Schedule #3 created by enrollment client\""
                            }
                };
                p.Start();

                // alternative way is to trigger the DeviceEnroller.exe from the scheduled task directly,
                // but when something changes I will miss maybe new args or other things..., staying with the task for the time being
                // Process.Start(Environment.ExpandEnvironmentVariables(@"%windir%\system32\DeviceEnroller.exe"), $"/o \"{_mdmDiagnostics.OmaDmAccountIdMMPC}\" /c /b");
            }
            catch (Exception ex)
            {
                MessageBox.Show("MMP-C Sync", "MMP-C Sync failed to start\n\n" + ex.ToString(), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            SyncMlProgress.NotInProgress = false;
            LabelStatus.Content = "Sync triggered";
            LabelStatus.Visibility = Visibility.Visible;
        }

        private void MenuItemResetSyncTriggerStatus_Click(object sender, RoutedEventArgs e)
        {
            SyncMlProgress.NotInProgress = true;
            LabelStatus.Content = "";
            LabelStatus.Visibility = Visibility.Hidden;
        }

        private void CheckBoxHtmlDecode_Checked(object sender, RoutedEventArgs e)
        {
            if (CheckBoxHtmlDecode.IsChecked == true)
            {
                TextEditorMessages.Text = TextEditorMessages.Text.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"");
            }
            else
            {
                TextEditorMessages.Text = ((SyncMlMessage)ListBoxMessages.SelectedItems[0]).Xml;
            }
        }

        private void ButtonClear_Click(object sender, RoutedEventArgs e)
        {
            SyncMlSessions.Clear();

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
            {
                return;
            }

            TextEditorMessages.Text = selectedItem.Xml;

            LabelMessageStats.Content = $"Message length: {selectedItem.Xml.Length}";

            bool wellFormatedXml = true;
            try
            {
                XElement.Parse(selectedItem.Xml);
            }
            catch (Exception)
            {
                wellFormatedXml = false;
            }
            
            if (selectedItem.Xml.Length > 60 * 1000 && !wellFormatedXml)
            {
                LabelTruncatedDataIndicator.Visibility = Visibility.Visible;

                Debug.WriteLine("Truncated XML detected, trying to format it...");
                // parse the truncated xml again with more robust parsing logic
                TextEditorMessages.Text = Helper.TryFormatTruncatedXml(selectedItem.Xml);
            }
            else
            {
                LabelTruncatedDataIndicator.Visibility = Visibility.Hidden;
            }

            try
            {
                _foldingStrategy.UpdateFoldings(_foldingManager, TextEditorMessages.Document);
            }
            catch (Exception)
            {
                // ignored
            }

            CheckBoxHtmlDecode.IsChecked = false;
        }

        private void ListBoxSessions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(ListBoxSessions.SelectedItem is SyncMlSession selectedItem))
            {
                return;
            }

            ListBoxMessages.ItemsSource = selectedItem.Messages;
            ListBoxMessages.Items.Refresh();

            if (ListBoxMessages.Items.Count > 0)
            {
                ListBoxMessages.SelectedIndex = 0;
            }
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
                using (var webClient = new System.Net.WebClient())
                {
                    var systemWebProxy = System.Net.WebRequest.GetSystemWebProxy();
                    systemWebProxy.Credentials = System.Net.CredentialCache.DefaultCredentials;
                    webClient.Proxy = systemWebProxy;
                    webClient.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);

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
                        LabelUpdateIndicator.Content = LabelUpdateIndicator.Content.ToString().Replace("[0.0.0]", version);
                        LabelUpdateIndicator.Visibility = Visibility.Visible;
                        _updateCheckInitial = false;
                        return;
                    }

                    LabelUpdateIndicator.Visibility = Visibility.Hidden;
                    ButtonRestartUpdate.Content = ButtonRestartUpdate.Content.ToString().Replace("[0.0.0]", version);

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

        private void MenuItemRegistryEnrollments_Click(object sender, RoutedEventArgs e)
        {
            Helper.OpenRegistry(@"Computer\HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Enrollments");
        }

        private void MenuItemRegistryProvisioning_Click(object sender, RoutedEventArgs e)
        {
            Helper.OpenRegistry(@"Computer\HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Provisioning");
        }

        private void MenuItemRegistryPolicyManager_OnClick(object sender, RoutedEventArgs e)
        {
            Helper.OpenRegistry(@"Computer\HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager");
        }

        private void MenuItemRegistryRebootRequiredUris_Click(object sender, RoutedEventArgs e)
        {
            Helper.OpenRegistry(@"Computer\HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Provisioning\SyncML\RebootRequiredURIs");
        }

        private void MenuItemRegistryDeclaredConfiguration_Click(object sender, RoutedEventArgs e)
        {
            Helper.OpenRegistry(@"Computer\HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\DeclaredConfiguration");
        }

        private void MenuItemRegistryEnterpriseDesktopAppManagement_Click(object sender, RoutedEventArgs e)
        {
            Helper.OpenRegistry(@"Computer\HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\EnterpriseDesktopAppManagement");
        }

        private void MenuItemRegistryIntuneManagementExtension_Click(object sender, RoutedEventArgs e)
        {
            Helper.OpenRegistry(@"Computer\HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\IntuneManagementExtension");
        }

        private async void MenuItemMdmDiagnostics_OnClick(object sender, RoutedEventArgs e)
        {
            LabelStatusTop.Visibility = Visibility.Visible;

            await Helper.RunMdmDiagnosticsTool("");

            LabelStatusTop.Visibility = Visibility.Hidden;
        }

        private async void MenuItemMdmDiagnosticsAutopilot_OnClick(object sender, RoutedEventArgs e)
        {
            LabelStatusTop.Visibility = Visibility.Visible;

            await Helper.RunMdmDiagnosticsTool("Autopilot");

            LabelStatusTop.Visibility = Visibility.Hidden;
        }

        private async void MenuItemMdmDiagnosticsDeviceEnrollment_OnClick(object sender, RoutedEventArgs e)
        {
            LabelStatusTop.Visibility = Visibility.Visible;

            await Helper.RunMdmDiagnosticsTool("DeviceEnrollment");

            LabelStatusTop.Visibility = Visibility.Hidden;
        }

        private async void MenuItemMdmDiagnosticsDeviceProvisioning_OnClick(object sender, RoutedEventArgs e)
        {
            LabelStatusTop.Visibility = Visibility.Visible;

            await Helper.RunMdmDiagnosticsTool("DeviceProvisioning");

            LabelStatusTop.Visibility = Visibility.Hidden;
        }

        private async void MenuItemMdmDiagnosticsTpm_OnClick(object sender, RoutedEventArgs e)
        {
            LabelStatusTop.Visibility = Visibility.Visible;

            await Helper.RunMdmDiagnosticsTool("TPM");

            LabelStatusTop.Visibility = Visibility.Hidden;
        }

        private void MenuItemBackgroundLogging_Click(object sender, RoutedEventArgs e)
        {
            if (menuItemBackgroundLogging.IsChecked)
            {
                Trace.Listeners.Add(new TextWriterTraceListener($"SyncMLStream-BackgroundLogging-{Environment.MachineName}-{DateTime.Now:MM-dd-yy_H-mm-ss}.xml", "listenerSyncMLStream"));
                Trace.AutoFlush = true;

                SyncMlSessions.Clear();
                ListBoxMessages.ItemsSource = null;

                TextEditorStream.Clear();
                TextEditorMessages.Clear();

                TextEditorStream.IsEnabled = false;
                TextEditorStream.AppendText(Environment.NewLine + "\t'Background Logging Mode' enabled.");
            }
            else
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
                        try
                        {
                            _foldingStrategy.UpdateFoldings(_foldingManager, TextEditorMessages.Document);
                        }
                        catch (Exception)
                        {
                            // ignored
                        }

                        var valueSessionId = "0";
                        var matchSessionId = new Regex("<SessionID>([0-9a-zA-Z]+)</SessionID>", RegexOptions.IgnoreCase).Match(valueSyncMl);
                        if (matchSessionId.Success)
                        {
                            valueSessionId = matchSessionId.Groups[1].Value;
                        }

                        if (!SyncMlSessions.Any(item => item.SessionId == valueSessionId))
                        {
                            var syncMlSession = new SyncMlSession(valueSessionId);
                            SyncMlSessions.Add(syncMlSession);
                        }

                        var valueMsgId = "0";
                        var matchMsgId = new Regex("<MsgID>([0-9]+)</MsgID>", RegexOptions.IgnoreCase).Match(valueSyncMl);
                        if (matchMsgId.Success)
                        {
                            valueMsgId = matchMsgId.Groups[1].Value;
                        }

                        var syncMlMessage = new SyncMlMessage(valueSessionId, valueMsgId, valueSyncMl);
                        SyncMlSessions.FirstOrDefault(item => item.SessionId == valueSessionId)?.Messages.Add(syncMlMessage);
                    }
                }
            }
        }

        private void ButtonSaveAs_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog fileDialog;

            if (TextEditorSyncMlRequests.IsVisible == true)
            {
                fileDialog = new SaveFileDialog
                {
                    Filter = "Xml files|*.xml|All files|*.*",
                    FilterIndex = 0,
                    DefaultExt = "xml",
                    AddExtension = true,
                    CheckPathExists = true,
                    RestoreDirectory = true,
                    Title = "Save SyncML requests",
                    FileName = $"SyncMLRequests-{Environment.MachineName}-{DateTime.Now:MM-dd-yy_H-mm-ss}.xml"
                };
                fileDialog.FileOk += (o, args) => File.WriteAllText(((FileDialog)o).FileName, TextEditorSyncMlRequests.Text);
            }
            else // Save normal SyncML stream
            {
                fileDialog = new SaveFileDialog
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
            }

            fileDialog.ShowDialog();
        }

        private void MenuItemAlwaysOnTop_Click(object sender, RoutedEventArgs e)
        {
            if (menuItemAlwaysOnTop.IsChecked)
            {
                Topmost = true;
            }
            else
            {
                Topmost = false;
            }
        }

        private void MenuItemStopEtwSession_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (TraceEventSession.IsElevated() != true)
                {
                    throw new InvalidOperationException("Collecting ETW trace events requires administrative privileges.");
                }

                if (TraceEventSession.GetActiveSessionNames().Contains(SessionName))
                {
                    Debug.WriteLine($"The session name '{SessionName}' is running, stopping existing session now.");
                    TraceEventSession.GetActiveSession(SessionName).Stop(true);

                    TraceEventSessionState.Started = false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception: {ex}");
            }
        }

        private void MenuItemShowAllChars_Click(object sender, RoutedEventArgs e)
        {
            if (menuItemShowAllChars.IsChecked)
            {
                TextEditorMessages.Options.ShowSpaces = true;
                TextEditorMessages.Options.ShowBoxForControlCharacters = true;
                TextEditorStream.Options.ShowSpaces = true;
                TextEditorStream.Options.ShowBoxForControlCharacters = true;
                TextEditorSyncMlRequests.Options.ShowSpaces = true;
                TextEditorSyncMlRequests.Options.ShowBoxForControlCharacters = true;
                TextEditorSyncMlRequestsRequestViewer.Options.ShowSpaces = true;
                TextEditorSyncMlRequestsRequestViewer.Options.ShowBoxForControlCharacters = true;
            }
            else
            {
                TextEditorMessages.Options.ShowSpaces = false;
                TextEditorMessages.Options.ShowBoxForControlCharacters = false;
                TextEditorStream.Options.ShowSpaces = false;
                TextEditorStream.Options.ShowBoxForControlCharacters = false;
                TextEditorSyncMlRequests.Options.ShowSpaces = false;
                TextEditorSyncMlRequests.Options.ShowBoxForControlCharacters = false;
                TextEditorSyncMlRequestsRequestViewer.Options.ShowSpaces = false;
                TextEditorSyncMlRequestsRequestViewer.Options.ShowBoxForControlCharacters = false;
            }
        }

        private void MenuItemWordWrap_Click(object sender, RoutedEventArgs e)
        {
            if (menuItemWordWrap.IsChecked)
            {
                TextEditorMessages.WordWrap = true;
                TextEditorStream.WordWrap = true;
                TextEditorSyncMlRequests.WordWrap = true;
                TextEditorSyncMlRequestsRequestViewer.WordWrap = true;
                TextEditorWifiProfiles.WordWrap = true;
                TextEditorVpnProfiles.WordWrap = true;
            }
            else
            {
                TextEditorMessages.WordWrap = false;
                TextEditorStream.WordWrap = false;
                TextEditorSyncMlRequests.WordWrap = false;
                TextEditorSyncMlRequestsRequestViewer.WordWrap = false;
                TextEditorWifiProfiles.WordWrap = false;
                TextEditorVpnProfiles.WordWrap = false;
            }
        }

        private void MenuItemDecodeBase64_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var text = string.Empty;
                var prettyJson = string.Empty;
                var resultText = string.Empty;
                bool isJson = false;

                if (TextEditorStream.IsVisible)
                {
                    text = TextEditorStream.SelectedText;                  
                }
                else if (TextEditorMessages.IsVisible)
                {
                    text = TextEditorMessages.SelectedText;
                }
                else if (TextEditorSyncMlRequests.IsVisible)
                {
                    text = TextEditorSyncMlRequests.SelectedText;
                }

                // try to be nice and remove some unwanted characters for higher success rate
                text = text.Replace(".", "");
                text = text.Replace("\n", "");
                text = text.Replace("\r", "");
                text = text.Replace("\t", "");

                // base64 test should be divisible by 4 or append =
                while (text.Length % 4 != 0)
                {
                    text += '=';
                }

                try
                {
                    text = Encoding.UTF8.GetString(Convert.FromBase64String(text));
                }
                catch (Exception)
                {
                    // prevent Exceptions for non-Base64 data
                }

                try
                {
                    prettyJson = JToken.Parse(text).ToString(Newtonsoft.Json.Formatting.Indented);
                    isJson = true;

                }
                catch (Exception)
                {
                    // prevent Exceptions for non-JSON data
                }
                if (string.IsNullOrEmpty(prettyJson))
                {
                    //Clipboard.SetText(text);
                    resultText = text;
                }
                else
                {
                    //Clipboard.SetText(prettyJson);
                    resultText = prettyJson;
                }

                DataEditor dataEditor = new DataEditor
                {
                    DataFromMainWindow = resultText,
                    JsonSyntax = isJson,
                    HideButonClear = true,
                    Title = "Data Editor - Base64 Decode",
                    TextEditorData = { ShowLineNumbers = false }
                };

                dataEditor.ShowDialog();
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private void LabelDeviceName_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Clipboard.SetText(LabelDeviceName.Content.ToString(), TextDataFormat.Text);
        }

        private void LabelBackToTop_MouseUp(object sender, MouseButtonEventArgs e)
        {
            TextEditorMessages.ScrollToHome();
        }

        private void MenuItemOpenImeLogs_Click(object sender, RoutedEventArgs e)
        {
            Helper.OpenFolder(@"C:\ProgramData\Microsoft\IntuneManagementExtension\Logs");
        }

        private void MenuItemOpenMDMDiagnosticsFolder_Click(object sender, RoutedEventArgs e)
        {
            Helper.OpenFolder(@"C:\Users\Public\Documents\MDMDiagnostics");
        }

        private void MenuItemOpenSystemProfileMDM_Click(object sender, RoutedEventArgs e)
        {
            Helper.OpenFolder(Path.Combine(Environment.SystemDirectory, @"Config\SystemProfile\AppData\Local\mdm"));
        }

        private void MenuItemOpenDeclaredConfigurationHostOSFolder_Click(object sender, RoutedEventArgs e)
        {
            Helper.OpenFolder(@"C:\ProgramData\microsoft\DC\HostOS");
        }

        private void ParseCommandlineArgs(string[] args)
        {
            if (args.Any())
            {
                foreach (var arg in args)
                {
                    if (arg.StartsWith("-") || arg.StartsWith("/"))
                    {
                        switch (arg.TrimStart(new[] { '-', '/' })[0].ToString().ToLower())
                        {
                            case "s":
                                _syncMDMSwitch = true;
                                break;
                            case "m":
                                _syncMMPCSwitch = true;
                                break;
                            case "b":
                                _backgroundLoggingSwitch = true;
                                break;
                            case "h":
                                _hideWhenMinimizedSwitch = true;
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ParseCommandlineArgs(Environment.GetCommandLineArgs());

            if (_backgroundLoggingSwitch)
            {
                menuItemBackgroundLogging.IsChecked = true;
            }

            if (_hideWhenMinimizedSwitch)
            {
                // prevent ballon tip on startup for commandline start
                _notifyIconBallonShownOnce = true;

                menuItemHideWhenMinimized.IsChecked = true;
                WindowState = WindowState.Minimized;
                Hide();
            }

            // only one MDM sync can be triggert at a time, so we check for the commandline args and trigger the sync
            // if both are set, the MDM sync is triggered
            if (_syncMDMSwitch)
            {
                ButtonMDMSync_Click(null, null);
            }
            else if (_syncMMPCSwitch)
            {
                ButtonMMPCSync_Click(null, null);
            }
        }

        private async void ButtonRunRequest_Click(object sender, RoutedEventArgs e)
        {
            _CmdIdCounter++;
            var syncML = string.Empty;
            var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            ButtonRunRequest.IsEnabled = false;

            // SyncML Editor
            if (CheckBoxUseSyncML.IsChecked == true)
            {
                syncML = TextEditorSyncMlRequestsRequestViewer.Text;
                try
                {
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(syncML);
                    XmlNode cmdIdNode = xmlDoc.SelectSingleNode("//CmdID");
                    if (cmdIdNode != null)
                    {
                        cmdIdNode.InnerText = _CmdIdCounter.ToString();
                    }
                    syncML = xmlDoc.InnerXml;
                }
                catch (Exception)
                {
                    MessageBox.Show("Invalid SyncML, proper SyncML starts with <SyncBody>...", "SyncML Viewer", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ButtonRunRequest.IsEnabled = true;
                    return;
                }
            }
            else // Assisted via comboboxes and textboxes
            {
                // no OMA URI or Data Format -> return
                if (string.IsNullOrEmpty(TextBoxUri.Text) || string.IsNullOrEmpty(ComboBoxFormat.Text))
                {
                    MessageBox.Show("URI and Data Format are required", "SyncML Viewer", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ButtonRunRequest.IsEnabled = true;
                    return;
                }

                // replace whitespaces with %20:
                // ./Device/Vendor/MSFT/DMClient/Provider/MS DM Server/FirstSyncStatus/SkipUserStatusPage
                // ./Device/Vendor/MSFT/DMClient/Provider/MS%20DM%20Server/FirstSyncStatus/SkipUserStatusPage
                var omaUri = TextBoxUri.Text.Replace(" ", "%20");
                TextBoxUri.Text = omaUri;

                syncML = "<SyncBody>\n" +
                        "<CMD-ITEM>\n" +
                            "<CmdID>CMDID-ITEM</CmdID>\n" +
                            "<Item>\n" +
                                "<Target>\n" +
                                    "<LocURI>OMAURI-ITEM</LocURI>\n" +
                                "</Target>\n" +
                                "<Meta>\n" +
                                    "<Format xmlns=\"syncml:metinf\">FORMAT-ITEM</Format>\n" +
                                    "<Type xmlns=\"syncml:metinf\">TYPE-ITEM</Type>\n" +
                                "</Meta>\n" +
                                "<Data>DATA-ITEM</Data>\n" +
                            "</Item>\n" +
                            "</CMD-ITEM>\n" +
                        "</SyncBody>";

                syncML = syncML.Replace("CMD-ITEM", ComboBoxCmd.Text);
                syncML = syncML.Replace("CMDID-ITEM", _CmdIdCounter.ToString());
                syncML = syncML.Replace("OMAURI-ITEM", omaUri);
                syncML = syncML.Replace("FORMAT-ITEM", ComboBoxFormat.Text);
                syncML = syncML.Replace("TYPE-ITEM", ComboBoxDataType.Text);
                syncML = syncML.Replace("DATA-ITEM", TextBoxData.Text);

                // remove data type if empty
                if (string.IsNullOrEmpty(ComboBoxDataType.Text))
                {
                    syncML = syncML.Replace("<Type xmlns=\"syncml:metinf\"></Type>", "");
                }
            }

            // try adding location URI to AutoCompleteModel
            if (!string.IsNullOrEmpty(TextBoxUri.Text) && TextBoxUri.Text.StartsWith("./"))
            {
                _autoCompleteModel.AddData(TextBoxUri.Text);
            }

            // we are writing the SyncML request input file to the disk
            var syncMlInputFile = Properties.Resources.InputFile;
            var syncMlInputFilePath = Path.Combine(assemblyPath, syncMlInputFile);
            try
            {
                File.WriteAllText(syncMlInputFilePath, TryFormatXml(syncML));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to write {syncMlInputFilePath} to disk, ex = {ex}");
            }

            TextEditorSyncMlRequestsRequestViewer.Text = TryFormatXml(syncML);

            TextEditorSyncMlRequests.Text += $"-------------------- Request {_CmdIdCounter} --------------------\n\n";
            TextEditorSyncMlRequests.Text += TryFormatXml(syncML);

            if (menuItemAutoScroll.IsChecked)
            {
                TextEditorSyncMlRequests.ScrollToEnd();
            }

            // We are extracting the Executer binary from the resources and write it to disk
            var binaryName = Properties.Resources.Executer;
            var pathExecuter = Path.Combine(assemblyPath, binaryName);

            var assembly = Assembly.GetExecutingAssembly();

            var resourceName = "SyncMLViewer." + Properties.Resources.Executer;
            using (Stream resourceStream = assembly.GetManifestResourceStream(resourceName))
            {
                if (resourceStream != null)
                {
                    byte[] buffer = new byte[resourceStream.Length];
                    resourceStream.Read(buffer, 0, buffer.Length);

                    try
                    {
                        File.WriteAllBytes(pathExecuter, buffer);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to write {binaryName} to disk, ex = {ex}");
                    }
                }
                else
                {
                    Debug.WriteLine($"Resource {resourceName} not found.");
                }
            }

            var hash = string.Empty;
            var resourceNameHash = "SyncMLViewer." + Properties.Resources.Executer + ".hash";
            using (Stream resourceStream = assembly.GetManifestResourceStream(resourceNameHash))
            {
                if (resourceStream != null)
                {
                    byte[] buffer = new byte[resourceStream.Length];
                    resourceStream.Read(buffer, 0, buffer.Length);

                    try
                    {
                        hash = Encoding.UTF8.GetString(buffer);
                        Debug.WriteLine($"{resourceName} expected hash: {hash}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to read {resourceNameHash} from resources, ex = {ex}");
                    }
                }
                else
                {
                    Debug.WriteLine($"Resource {resourceNameHash} not found.");
                }
            }

            var fileHashString = CalculateSha256FileHash(pathExecuter);

            // Compare the calculated hash with the provided hash from resources
            bool hashesMatch = string.Equals(fileHashString, hash.Trim('\r', '\n', ' '), StringComparison.OrdinalIgnoreCase);
            if (hashesMatch)
            {
                Debug.WriteLine($"{resourceName} file hash: {fileHashString} equals expected hash");
            }
            else
            {
                MessageBox.Show($"The {Properties.Resources.Executer} binary has been tampered with, can't start request.", "Executer binary tampered", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"{Properties.Resources.Executer} file hash: {fileHashString} does not not match expected hash: {hash}");
                return;
            }

            // build the arguments for the Executer binary
            var arguments = $"-SyncMLFile \"{syncMlInputFilePath}\"";

            if (menuItemKeepLocalMDMEnrollment.IsChecked)
            {
                arguments = $"-SyncMLFile \"{syncMlInputFilePath}\" -KeepLocalMDMEnrollment";
            }

            var resultOutput = string.Empty;
            var resultError = string.Empty;

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // Start Executer process
            using (var p = new Process
            {
                StartInfo =
                    {
                        UseShellExecute = false,
                        FileName = pathExecuter,
                        Arguments = arguments,
                        CreateNoWindow = true,
                        //RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    }
            })
            {
                //p.OutputDataReceived += (o, args) => { resultOutput += args.Data + Environment.NewLine; };
                p.ErrorDataReceived += (o, args) => { resultError += args.Data + Environment.NewLine; };

                p.Start();

                //p.BeginOutputReadLine();
                p.BeginErrorReadLine();

                Task processExited = WaitForExitAsync(p);

                await processExited;
            }

            stopwatch.Stop();
            if (menuItemExecutionTime.IsChecked)
            {
                LabelProcessingTime.Content = string.Format("{0:0.000}s", stopwatch.Elapsed.TotalSeconds);
            }
            else
            {
                LabelProcessingTime.Content = string.Empty;
            }

            // we are reading the Executer SyncML Request Output file from the disk
            var syncMlOutputFilePath = Path.Combine(assemblyPath, Properties.Resources.OutputFile);
            try
            {
                resultOutput += File.ReadAllText(syncMlOutputFilePath);
            }
            catch (Exception ex)
            {
                TextEditorSyncMlRequests.Text = "Failed to read " + syncMlOutputFilePath + " from disk, ex = " + ex;
            }
            
            TextEditorSyncMlRequests.Text += $"\n\n-------------------- Response {_CmdIdCounter} -------------------\n\n";
            TextEditorSyncMlRequests.Text += resultOutput;
            TextEditorSyncMlRequests.Text += "\n" + resultError + "\n";

            if (menuItemAutoScroll.IsChecked)
            {
                TextEditorSyncMlRequests.ScrollToEnd();
            }

            ButtonRunRequest.IsEnabled = true;
        }

        private Task WaitForExitAsync(Process process)
        {
            var tcs = new TaskCompletionSource<object>();

            process.EnableRaisingEvents = true;
            process.Exited += (sender, e) => tcs.SetResult(null);

            return tcs.Task;
        }

        private string CalculateSha256FileHash(string filePath)
        {
            using (var hashAlgorithmProvider = SHA256.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    byte[] hashBytes = hashAlgorithmProvider.ComputeHash(stream);
                    return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                }
            }
        }

        private async void RunExecuter(string arguments)
        {
            var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            // We are extracting the Executer binary from the resources and write it to disk
            var binaryName = Properties.Resources.Executer;
            var path = Path.Combine(assemblyPath, binaryName);

            var assembly = Assembly.GetExecutingAssembly();

            var resourceName = "SyncMLViewer." + Properties.Resources.Executer;
            using (Stream resourceStream = assembly.GetManifestResourceStream(resourceName))
            {
                if (resourceStream != null)
                {
                    byte[] buffer = new byte[resourceStream.Length];
                    resourceStream.Read(buffer, 0, buffer.Length);

                    try
                    {
                        File.WriteAllBytes(path, buffer);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to write {binaryName} to disk, ex = {ex}");
                    }
                }
                else
                {
                    Debug.WriteLine($"Resource {resourceName} not found.");
                }
            }

            var resultOutput = string.Empty;
            var resultError = string.Empty;

            using (var p = new Process
            {
                StartInfo =
                    {
                        UseShellExecute = false,
                        FileName = path,
                        Arguments = arguments,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    }
            })
            {
                p.OutputDataReceived += (o, args) => { resultOutput += args.Data + Environment.NewLine; };
                p.ErrorDataReceived += (o, args) => { resultError += args.Data + Environment.NewLine; };

                p.Start();

                p.BeginOutputReadLine();
                p.BeginErrorReadLine();

                Task processExited = WaitForExitAsync(p);

                await processExited;
            }
        }

        private void CheckBoxUseSyncML_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBoxUseSyncML.IsChecked == true)
            {
                ComboBoxCmd.IsEnabled = false;
                ComboBoxFormat.IsEnabled = false;
                ComboBoxDataType.IsEnabled = false;
                TextBoxData.IsEnabled = false;
                TextBoxUri.IsEnabled = false;
                LabelEditor.IsEnabled = false;
                LabelEditor.Foreground = Brushes.Gray;
                TextEditorSyncMlRequestsRequestViewer.IsReadOnly = false;
                TextEditorSyncMlRequestsRequestViewer.Options.HighlightCurrentLine = true;
            }
            else
            {
                ComboBoxCmd.IsEnabled = true;
                ComboBoxFormat.IsEnabled = true;
                ComboBoxDataType.IsEnabled = true;
                TextBoxData.IsEnabled = true;
                TextBoxUri.IsEnabled = true;
                LabelEditor.IsEnabled = true;
                LabelEditor.Foreground = new SolidColorBrush(Color.FromRgb(0, 42, 248));
                TextEditorSyncMlRequestsRequestViewer.IsReadOnly = true;
                TextEditorSyncMlRequestsRequestViewer.Options.HighlightCurrentLine = false;
                //TextEditorSyncMlRequestsRequestViewer.Clear();
            }
        }

        private void TextBoxUri_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            bool found = false;
            var border = (resultStack.Parent as ScrollViewer).Parent as Border;

            if (e.Key == Key.Enter)
            {
                ButtonRunRequest_Click(null, null);
                resultStack.Children.Clear();
                border.Visibility = Visibility.Collapsed;
                return;
            }

            if (e.Key == Key.Escape)
            {
                resultStack.Children.Clear();
                border.Visibility = Visibility.Collapsed;
                return;
            }

            var data = _autoCompleteModel.GetData();

            var query = (sender as TextBox).Text;

            if (query.Length == 0)
            {
                // Clear   
                resultStack.Children.Clear();
                border.Visibility = Visibility.Collapsed;
            }
            else
            {
                border.Visibility = Visibility.Visible;
            }

            // Clear the list   
            resultStack.Children.Clear();

            // Add the result   
            foreach (var obj in data)
            {
                if (obj.ToLower().StartsWith(query.ToLower()))
                {
                    // The word starts with this... Autocomplete must work   
                    AddTextBlockItem(obj);
                    found = true;
                }
            }

            if (!found)
            {
            //    resultStack.Children.Add(new TextBlock() { Text = "No results found." });
            //    //resultStack.Children.Clear();
                border.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        private void AddTextBlockItem(string text)
        {
            TextBlock block = new TextBlock
            {
                Text = text,
                Margin = new Thickness(2, 3, 2, 3),
                Cursor = Cursors.Hand
            };

            // Mouse events   
            block.MouseLeftButtonUp += (sender, e) =>
            {
                TextBoxUri.Text = (sender as TextBlock).Text;
                var border = (resultStack.Parent as ScrollViewer).Parent as Border;
                border.Visibility = Visibility.Collapsed;
            };

            block.MouseEnter += (sender, e) =>
            {
                TextBlock b = sender as TextBlock;
                b.Background = Brushes.PeachPuff;
            };

            block.MouseLeave += (sender, e) =>
            {
                TextBlock b = sender as TextBlock;
                b.Background = Brushes.Transparent;
            };

            // Add to the panel   
            resultStack.Children.Add(block);
        }

        private void MenuItemClearHistoryItems_Click(object sender, RoutedEventArgs e)
        {
            resultStack.Children.Clear();
            _autoCompleteModel.ClearData();
        }

        private void HideAutoCompleteStackPanel(object sender, RoutedEventArgs e)
        {
            var border = (resultStack.Parent as ScrollViewer).Parent as Border;
            border.Visibility = Visibility.Collapsed;
        }

        private void LabelToBottom_MouseUp(object sender, MouseButtonEventArgs e)
        {
            TextEditorSyncMlRequests.ScrollToEnd();
        }

        private void MenuItemOpenHelp_Click(object sender, RoutedEventArgs e)
        {
            Helper.OpenUrl("http://aka.ms/CSPList");
        }

        private void LabelEditor_MouseUp(object sender, MouseButtonEventArgs e)
        {
            DataEditor dataEditor = new DataEditor
            {
                DataFromMainWindow = TextBoxData.Text
            };

            dataEditor.ShowDialog();

            TextBoxData.Text = dataEditor.DataFromSecondWindow;
        }

        private void MenuItemSetEmbeddedMode_Click(object sender, RoutedEventArgs e)
        {
            RunExecuter("-SetEmbeddedMode true");
        }

        private void MenuItemClearEmbeddedMode_Click(object sender, RoutedEventArgs e)
        {
            RunExecuter("-SetEmbeddedMode false");
        }

        private void MenuItemDecodeHTML_Click(object sender, RoutedEventArgs e)
        {
            var text = string.Empty;

            if (TextEditorStream.IsVisible)
            {
                text = TextEditorStream.SelectedText;
            }
            else if (TextEditorMessages.IsVisible)
            {
                text = TextEditorMessages.SelectedText;
            }
            else if (TextEditorSyncMlRequests.IsVisible)
            { 
                text = TextEditorSyncMlRequests.SelectedText;
            }
            
            var decodedText = HttpUtility.HtmlDecode(text);
            //Clipboard.SetText(decodedText);

            DataEditor dataEditor = new DataEditor
            {
                DataFromMainWindow = decodedText,
                HideButonClear = true,
                Title = "Data Editor - HTML Decode",
                TextEditorData = { ShowLineNumbers = false, SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("XML") }
            };

            dataEditor.ShowDialog();
        }

        private void LabelFormat_MouseUp(object sender, MouseButtonEventArgs e)
        {
            TextEditorSyncMlRequestsRequestViewer.Text = TryFormatXml(TextEditorSyncMlRequestsRequestViewer.Text).Trim(' ');
        }

        private void TabControlSyncMlViewer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl tabControl && tabControl.SelectedItem is TabItem selectedTab)
            {
                // Handle the selected TabItem
                var tabName = selectedTab.Header.ToString();
                if (string.Compare(tabName, "SyncML Sessions/Messages", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    if (ListBoxMessages.SelectedItem != null)
                    {
                        var selectedItem = (SyncMlMessage)ListBoxMessages.SelectedItem;
                        if (selectedItem.Xml.Length > 60 * 1000)
                        {
                            LabelTruncatedDataIndicator.Visibility = Visibility.Visible;
                        }
                    }
                }
                else
                {
                    LabelTruncatedDataIndicator.Visibility = Visibility.Hidden;
                }
            }
        }

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

        private void ButtonRefreshWifi_Click(object sender, RoutedEventArgs e)
        {
            WifiProfileList.Clear();

            var output = Helper.RunCommand("netsh", "wlan show interfaces");
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

        private void ButtonRefreshVpn_Click(object sender, RoutedEventArgs e)
        {
            VpnProfileList.Clear();

            using (var ps = PowerShell.Create())
            {
                ps.AddCommand("Get-VpnConnection");
                foreach (var item in ps.Invoke())
                {
                    var name = item.Members["Name"].Value as string;
                    var xml = item.Members["VpnConfigurationXml"].Value as string;
                    VpnProfileList.Add(new VpnProfile(name, xml));
                }
            }

            ListBoxVpn.Items.Refresh();
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

            dataEditor.ShowDialog();
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

            dataEditor.ShowDialog();
        }

        private void LabelBackToTopWifi_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            TextEditorWifiProfiles.ScrollToHome();
        }

        private void LabelBackToTopVpn_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            TextEditorVpnProfiles.ScrollToHome();
        }

        private async void MenuItemRunMdmAdvancedDiagnosticReport_Click(object sender, RoutedEventArgs e)
        {
            await Helper.CreateAdvancedDiagnosticsReport();
        }

        private void MenuItemOpenMdmEventLog_Click(object sender, RoutedEventArgs e)
        {
            Helper.OpenEventLog("Microsoft-Windows-DeviceManagement-Enterprise-Diagnostics-Provider/Admin");
        }

        private void MenuItemFeedback_Click(object sender, RoutedEventArgs e)
        {
            Helper.OpenUrl("https://github.com/okieselbach/SyncMLViewer/issues");
        }

        private void MenuItemLookupStatusCode_Click(object sender, RoutedEventArgs e)
        {
            var text = string.Empty;

            if (TextEditorStream.IsVisible)
            {
                text = TextEditorStream.SelectedText;
            }
            else if (TextEditorMessages.IsVisible)
            {
                text = TextEditorMessages.SelectedText;
            }
            else if (TextEditorSyncMlRequests.IsVisible)
            {
                text = TextEditorSyncMlRequests.SelectedText;
            }

            var statusCode = string.Empty;

            // parse selected text and look for status code
            var matchStatusCode = new Regex(@"[^0-9]*([0-9]+)[^0-9]*", RegexOptions.IgnoreCase).Match(text);
            if (matchStatusCode.Success)
            {
                statusCode = matchStatusCode.Groups[1].Value;
            }

            var lookupSource = "MDM Status Code";

            // lookup MDM status code
            var result = _statusCodeLookupModel.GetDescription(statusCode);

            // alternative lookup error code Windows known error messages
            if (string.Compare(result, "Unknown status code.", StringComparison.OrdinalIgnoreCase) == 0)
            {
                Debug.WriteLine("Fallback to Windows Error Messages DB and assuming hex error code");
                if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    text = text.Substring(2);
                    if (uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint statusCodeUint))
                    {
                        lookupSource = "Win32 Error Code (hex)";
                        result = Helper.GetErrorMessage(statusCodeUint);
                    }
                }
                else
                {
                    if (uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint statusCodeUint))
                    {
                        lookupSource = "Win32 Error Code (int)";
                        result = Helper.GetErrorMessage(statusCodeUint);
                    }
                }
                
            }

            DataEditor dataEditor = new DataEditor
            {
                DataFromMainWindow = result,
                HideButonClear = true,
                Title = $"Data Editor - {lookupSource} lookup",
                TextEditorData = { ShowLineNumbers = false }
            };

            dataEditor.ShowDialog();
        }

        private void MenuItemDecodeCertificate_Click(object sender, RoutedEventArgs e)
        {
            var text = string.Empty;

            if (TextEditorStream.IsVisible)
            {
                text = TextEditorStream.SelectedText;
            }
            else if (TextEditorMessages.IsVisible)
            {
                text = TextEditorMessages.SelectedText;
            }
            else if (TextEditorSyncMlRequests.IsVisible)
            {
                text = TextEditorSyncMlRequests.SelectedText;
            }

            try
            {
                // try to decode PEM certificate, maybe it's a certificate :-D
                var resultText = Helper.DecodePEMCertificate(text);
                if (!string.IsNullOrEmpty(resultText))
                {
                    text = resultText;
                }
            }
            catch (Exception)
            {
                // prevent Exceptions for non-Base64 data
            }

            DataEditor dataEditor = new DataEditor
            {
                DataFromMainWindow = text,
                HideButonClear = true,
                Title = "Data Editor - Certificate Decode",
                TextEditorData = { ShowLineNumbers = false }
            };

            dataEditor.ShowDialog();
        }
    }
}


