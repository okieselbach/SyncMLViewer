using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
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
        // Inspired by M.Niehaus blog: https://oofhours.com/2019/07/25/want-to-watch-the-mdm-client-activity-in-real-time/

        // https://gist.githubusercontent.com/mattifestation/04e8299d8bc97ef825affe733310f7bd/raw/857bfbb31d0e12a8ebc48a95f95d298222bae1f6/NiftyETWProviders.json
        // ProviderName: Microsoft.Windows.DeviceManagement.OmaDmClient
        private static readonly Guid OmaDmClient = new Guid("{0EC685CD-64E4-4375-92AD-4086B6AF5F1D}");

        // https://docs.microsoft.com/en-us/windows/client-management/mdm/diagnose-mdm-failures-in-windows-10
        // Microsoft-WindowsPhone-Enterprise-Diagnostics-Provider
        private static readonly Guid EnterpriseDiagnosticsProvider = new Guid("{3B9602FF-E09B-4C6C-BC19-1A3DFA8F2250}");

        private const string SessionName = "SyncMLViewer";

        private bool decode = false;

        public MainWindow()
        {
            InitializeComponent();

            var backgroundWorker = new BackgroundWorker()
            {
                WorkerReportsProgress = true
            };
            backgroundWorker.DoWork += WorkerTraceEvents;
            backgroundWorker.ProgressChanged += WorkerProgressChanged;
            backgroundWorker.RunWorkerAsync();
        }

        private void WorkerTraceEvents(object sender, DoWorkEventArgs e)
        {
            try
            {
                if (TraceEventSession.IsElevated() != true)
                    throw new InvalidOperationException("Collecting ETW trace events requires administrative privileges.");

                if (TraceEventSession.GetActiveSessionNames().Contains(SessionName))
                    throw new InvalidOperationException($"The session name '{SessionName}' is already in use.");

                using (var traceEventSession = new TraceEventSession(SessionName))
                {
                    traceEventSession.StopOnDispose = true;
                    using (var traceEventSource = new ETWTraceEventSource(SessionName, TraceEventSourceType.Session))
                    {
                        traceEventSession.EnableProvider(OmaDmClient);
                        traceEventSession.EnableProvider(EnterpriseDiagnosticsProvider);

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

                //AppendText(userState.EventName);

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

                    var prettyDataText = TryFormatXml(dataText.Substring(startIndex, dataText.Length - startIndex - 1), decode);
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
                if (htmlDecode)
                {
                    //return WebUtility.HtmlDecode(XElement.Parse(text).ToString());
                    return XElement.Parse(text).ToString().Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"");
                }
                else
                {
                    return XElement.Parse(text).ToString();
                }
            }
            catch (Exception)
            {
                return text;
            }
        }

        private void AppendText(string text)
        {
            mainTextBox.Text = mainTextBox.Text + Environment.NewLine + text + Environment.NewLine;
        }

        private void ButtonSync_Click(object sender, RoutedEventArgs e)
        {
            using (var registryKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default)
                .OpenSubKey(@"SOFTWARE\Microsoft\Provisioning\OMADM\Accounts"))
            {
                if (registryKey == null) return;
                var id = ((IEnumerable<string>) registryKey.GetSubKeyNames()).First<string>();
                Process.Start($"{Environment.SystemDirectory}\\DeviceEnroller.exe", $"/o \"{id}\" /c /b");
            }
        }

        private void CheckBoxHtmlDecode_Checked(object sender, RoutedEventArgs e)
        {
            if (((CheckBox)sender).IsChecked == true)
            {
                decode = true;
                mainTextBox.Text = TryFormatXml(mainTextBox.Text, decode);
            }
        }
    }
}

