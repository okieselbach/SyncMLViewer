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

namespace SyncMLViewer
{
    /// <summary>
    /// Interaction logic for DataEditor.xaml
    /// </summary>
    public partial class DataEditor : Window
    {
        public string DataFromMainWindow { get; set; }

        public bool JsonSyntax { get; set; }

        public bool HideButonClear { get; set; }

        public string DataFromSecondWindow { get; private set; }

        public DataEditor()
        {
            InitializeComponent();
        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            DataFromSecondWindow = TextEditorData.Text;

            Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            TextEditorData.Text = DataFromMainWindow;
            TextEditorData.Options.HighlightCurrentLine = true;
            TextEditorData.Options.EnableRectangularSelection = true;
            TextEditorData.Options.EnableTextDragDrop = true;

            if (JsonSyntax)
            {
                using (var stream = Assembly.GetAssembly(typeof(ICSharpCode.AvalonEdit.TextEditor)).GetManifestResourceStream("ICSharpCode.AvalonEdit.Highlighting.Resources.JavaScript-Mode.xshd"))
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
    }
}
