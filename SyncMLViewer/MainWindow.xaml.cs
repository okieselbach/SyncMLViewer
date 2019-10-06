using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;
using Microsoft.Win32;

namespace SyncMLViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Inspired by Michael Niehaus - @mniehaus - blog about monitoring realtime MDM activity
        // https://oofhours.com/2019/07/25/want-to-watch-the-mdm-client-activity-in-real-time/

        // [MS-MDM]: Mobile Device Management Protocol
        // https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-mdm/

        // OMA DM protocol support - Get all the details how it is working...
        // https://docs.microsoft.com/en-us/windows/client-management/mdm/oma-dm-protocol-support

        // SyncML response status codes
        // https://docs.microsoft.com/en-us/windows/client-management/mdm/oma-dm-protocol-support#syncml-response-codes

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
        // interestingly it seems not to be needed...
        //private static readonly Guid EnterpriseDiagnosticsProvider = new Guid("{3da494e4-0fe2-415C-b895-fb5265c5c83b}");

        // for a tool we have too many libraries we can use ILMerge to combine them to a single assembly or embed them in the resources
        // https://www.nuget.org/packages/ilmerge
        // https://blogs.msdn.microsoft.com/microsoft_press/2010/02/03/jeffrey-richter-excerpt-2-from-clr-via-c-third-edition/


        private const string SessionName = "SyncMLViewer";
        private readonly BackgroundWorker _backgroundWorker;
        private readonly Runspace _rs;
        private bool _decode = false;

        private bool _firstHit = true;

        public MainWindow()
        {
            InitializeComponent();

            _rs = RunspaceFactory.CreateRunspace();
            _rs.Open();

            _backgroundWorker = new BackgroundWorker
            {
                WorkerReportsProgress = true
            };
            _backgroundWorker.DoWork += WorkerTraceEvents;
            _backgroundWorker.ProgressChanged += WorkerProgressChanged;
            _backgroundWorker.RunWorkerAsync();
        }

        private static void WorkerTraceEvents(object sender, DoWorkEventArgs e)
        {
            try
            {
                if (TraceEventSession.IsElevated() != true)
                    throw new InvalidOperationException("Collecting ETW trace events requires administrative privileges.");

                if (TraceEventSession.GetActiveSessionNames().Contains(SessionName))
                    throw new InvalidOperationException($"The session name '{SessionName}' is already in use.");

                // An End-To-End ETW Tracing Example: EventSource and TraceEvent
                // https://blogs.msdn.microsoft.com/vancem/2012/12/20/an-end-to-end-etw-tracing-example-eventsource-and-traceevent/
                using (var traceEventSession = new TraceEventSession(SessionName))
                {
                    traceEventSession.StopOnDispose = true;
                    using (var traceEventSource = new ETWTraceEventSource(SessionName, TraceEventSourceType.Session))
                    {
                        traceEventSession.EnableProvider(OmaDmClient);
                        traceEventSession.EnableProvider(OmaDmClientProvider);

                        new RegisteredTraceEventParser(traceEventSource).All += (data => (sender as BackgroundWorker).ReportProgress(0, data.Clone()));
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
                    throw new ArgumentException("No TraceEvent received");

                // show all events
                //AppendText(userState.EventName);

                // we are interested in just a few events with relevant data
                if (string.Equals(userState.EventName, "OmaDmClientExeStart", StringComparison.CurrentCultureIgnoreCase) || 
                    string.Equals(userState.EventName, "OmaDmSyncmlVerboseTrace", StringComparison.CurrentCultureIgnoreCase))
                {
                    string dataText = null;
                    try
                    {
                        dataText = Encoding.UTF8.GetString(userState.EventData());
                    }
                    catch (Exception)
                    {
                        // ignored
                    }

                    if (dataText == null) return;

                    var startIndex = dataText.IndexOf("<SyncML");
                    if (startIndex == -1) return;

                    var prettyDataText = TryFormatXml(dataText.Substring(startIndex, dataText.Length - startIndex - 1), _decode);
                    AppendText(prettyDataText);
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private static string TryFormatXml(string text, bool htmlDecode = false)
        {
            try
            {
                // HtmlDecode did too much here... WebUtility.HtmlDecode(XElement.Parse(text).ToString());
                return htmlDecode ? XElement.Parse(text).ToString().Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"") : XElement.Parse(text).ToString();
            }
            catch (Exception)
            {
                return text;
            }
        }

        private void AppendText(string text)
        {
            //mainTextBox.Text = $"{mainTextBox.Text}{Environment.NewLine}{text}{Environment.NewLine}";
            mainTextBox.Document.Blocks.Add(new Paragraph(new Run($"{text}")));
        }

        private void ButtonSync_Click(object sender, RoutedEventArgs e)
        {
            // trigger MDM sync via scheduled task with PowerShell
            // https://oofhours.com/2019/09/28/forcing-an-mdm-sync-from-a-windows-10-client/

            using (var ps = PowerShell.Create())
            {
                ps.Runspace = _rs;
                ps.AddScript("Get-ScheduledTask | ? {$_.TaskName -eq 'PushLaunch'} | Start-ScheduledTask");
                var returnedObject = ps.Invoke();
            }

            // Alternate implementation... was not working...
            //using (var registryKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default).OpenSubKey(@"SOFTWARE\Microsoft\Provisioning\OMADM\Accounts"))
            //{
            //    if (registryKey == null) return;
            //    var id = ((IEnumerable<string>) registryKey.GetSubKeyNames()).First<string>();
            //    Process.Start($"{Environment.SystemDirectory}\\DeviceEnroller.exe", $"/o \"{id}\" /c /b");
            //}
        }

        private void CheckBoxHtmlDecode_Checked(object sender, RoutedEventArgs e)
        {
            if (((CheckBox)sender).IsChecked == true)
            {
                _decode = true;
            }
        }

        private void ButtonClear_Click(object sender, RoutedEventArgs e)
        {
            //mainTextBox.Clear();
            mainTextBox.Document.Blocks.Clear();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            TraceEventSession.GetActiveSession(SessionName).Stop(true);
            _backgroundWorker.Dispose();
        }

        private void ButtonSearch_Click(object sender, RoutedEventArgs e)
        {
            var textRange = new TextRange(mainTextBox.Document.ContentStart, mainTextBox.Document.ContentEnd);
            textRange.ClearAllProperties();
            labelSearchStatus.Content = "";

            var textBoxText = textRange.Text;
            var searchText = textBoxSearch.Text;

            if (string.IsNullOrWhiteSpace(textBoxText) || string.IsNullOrWhiteSpace(searchText))
            {
                labelSearchStatus.Content = "Search text is missing!";
            }
            else
            {
                var regex = new Regex(searchText);
                var countMatchFound = Regex.Matches(textBoxText, regex.ToString(), RegexOptions.IgnoreCase).Count;

                if (countMatchFound > 0)
                {
                    if (countMatchFound > 1)
                    {
                        buttonSearch.Content = "Find next";
                    }

                    for (var startPointer = mainTextBox.Document.ContentStart;
                        startPointer.CompareTo(mainTextBox.Document.ContentEnd) <= 0;
                        startPointer = startPointer.GetNextContextPosition(LogicalDirection.Forward))
                    {
                        if (startPointer.CompareTo(mainTextBox.Document.ContentEnd) == 0)
                        {
                            break;
                        }

                        var parsedString = startPointer.GetTextInRun(LogicalDirection.Forward);
                        var indexOfParseString = parsedString.ToLower().IndexOf(searchText.ToLower());

                        if (indexOfParseString >= 0)
                        {
                            startPointer = startPointer.GetPositionAtOffset(indexOfParseString);

                            // we found the string
                            if (startPointer != null)
                            {
                                var nextPointer = startPointer.GetPositionAtOffset(searchText.Length);
                                var searchedTextRange = new TextRange(startPointer, nextPointer);

                                searchedTextRange.ApplyPropertyValue(TextElement.BackgroundProperty, new SolidColorBrush(Colors.Yellow));

                                if (_firstHit)
                                {
                                    var r = startPointer.GetCharacterRect(LogicalDirection.Backward);
                                    mainTextBox.ScrollToVerticalOffset(r.Y);
                                    _firstHit = false;
                                }
                            }
                        }
                    }
                }

                labelSearchStatus.Content = countMatchFound > 0 ? $"{countMatchFound} matches found." : "Nothing found!";
            }
        }

        public static void SetText(RichTextBox richTextBox, string text)
        {
            richTextBox.Document.Blocks.Clear();
            richTextBox.Document.Blocks.Add(new Paragraph(new Run(text)));
        }

        public static string GetText(RichTextBox richTextBox)
        {
            return new TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd).Text;
        }

        private void TextBoxSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textRange = new TextRange(mainTextBox.Document.ContentStart, mainTextBox.Document.ContentEnd);
            var textBoxText = textRange.Text;
            var searchText = textBoxSearch.Text;

            if (string.IsNullOrWhiteSpace(textBoxText) || string.IsNullOrWhiteSpace(searchText))
            {
                textRange.ClearAllProperties();
                labelSearchStatus.Content = "";
                buttonSearch.Content = "Search";
                _firstHit = true;
            }
        }

        private void ButtonSaveAs_Click(object sender, RoutedEventArgs e)
        {
            FileDialog fileDialog = new SaveFileDialog();
            fileDialog.Filter = "All files|*.*";
            fileDialog.FilterIndex = 0;
            fileDialog.DefaultExt = "txt";
            fileDialog.AddExtension = true;
            fileDialog.CheckPathExists = true;
            fileDialog.Title = "Save SyncML stream";
            fileDialog.FileOk += (o, args) =>
            {
                File.WriteAllText(((FileDialog) o).FileName, GetText(mainTextBox));
            };
            fileDialog.ShowDialog();
        }
    }
}

