using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Highlighting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using static SyncMLViewer.MainWindow;
using static System.Net.Mime.MediaTypeNames;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Search;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

namespace SyncMLViewer
{
    /// <summary>
    /// Interaction logic for DataEditor.xaml
    /// </summary>
    public partial class DataEditor : Window
    {
        public string DataFromMainWindow { get; set; }

        public string DataFromSecondWindow { get; private set; }

        public bool JsonSyntax { get; set; }

        public bool HideButonClear { get; set; }

        public ICommand DecodeBase64Command { get; }
        public ICommand DecodeCertCommand {  get; }

        public DataEditor()
        {
            InitializeComponent();

            SearchPanel.Install(TextEditorData);
            TextEditorData.Options.HighlightCurrentLine = true;
            TextEditorData.Options.EnableRectangularSelection = true;
            TextEditorData.Options.EnableTextDragDrop = true;

            DecodeCertCommand = new RelayCommand(() =>
            {
                var text = TextEditorData.SelectedText;

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
            });

            DecodeBase64Command = new RelayCommand(() => {

                string text = TextEditorData.SelectedText;
                string prettyJson = string.Empty;
                string resultText = string.Empty;
                bool isJson = false;

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
                    prettyJson = JToken.Parse(text).ToString(Formatting.Indented);
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
            });

            // a little hacky, setting DataContext (ViewModel) of the window to this class MainWindow
            DataContext = this;
        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            DataFromSecondWindow = TextEditorData.Text;

            Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            TextEditorData.Text = DataFromMainWindow;

            TextEditorData.TextArea.Focus();

            if (JsonSyntax)
            {
                using (var stream = Assembly.GetAssembly(typeof(TextEditor)).GetManifestResourceStream("ICSharpCode.AvalonEdit.Highlighting.Resources.JavaScript-Mode.xshd"))
                {
                    using (var reader = new System.Xml.XmlTextReader(stream))
                    {
                        TextEditorData.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                    }
                }
            }

            if (HideButonClear)
            {
                ButtonClear.Visibility = Visibility.Hidden;
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            DataFromSecondWindow = TextEditorData.Text;
        }

        private void CheckBoxWordWrap_Checked(object sender, RoutedEventArgs e)
        {
            TextEditorData.WordWrap = true;
        }

        private void CheckBoxWordWrap_Unchecked(object sender, RoutedEventArgs e)
        {
            TextEditorData.WordWrap = false;
        }

        private void ButtonClear_Click(object sender, RoutedEventArgs e)
        {
            TextEditorData.Clear();
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DataFromSecondWindow = TextEditorData.Text;
                // TODO: check if SearchPanel is open, if yes we do not handle close as the first esc is for closing the search panel
                // but the problem ist the IsVisible property is not set to false... bug in AvalonEdit?
                Close();
            }
        }

        private void LabelFormat_MouseUp(object sender, MouseButtonEventArgs e)
        {
            TextEditorData.Text = TryFormat(TextEditorData.Text).Trim();
        }

        private string TryFormat(string text)
        {
            // try to format the text as XML or JSON

            try
            {
                // HtmlDecode did too much here... WebUtility.HtmlDecode(XElement.Parse(text).ToString());
                var result = XElement.Parse(text.Trim()).ToString();
                TextEditorData.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("XML");
                return result;
            }
            catch (Exception)
            {
                // ignored
            }

            try
            {
                var result = JToken.Parse(text.Trim()).ToString(Formatting.Indented);
                TextEditorData.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("JSON");
                return result;
            }
            catch (Exception)
            {
                // ignored
            }
            
            return text;
        }
    }
}
