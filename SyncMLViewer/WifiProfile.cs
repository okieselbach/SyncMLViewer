using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using System.Windows.Input;

namespace SyncMLViewer
{
    public class WifiProfile : BindableBase
    {
        private string name;

        private string xml;

        public string Name
        {
            get
            {
                return name;
            }
            set
            {
                name = value;
                OnPropertyChanged("Name");
            }
        }

        public string Xml
        {
            get
            {
                return xml;
            }
            set
            {
                xml = value;
                OnPropertyChanged("Xml");
            }
        }

        public WifiProfile(string name, string xml)
        {
            Name = name;
            Xml = xml;
        }

        public override string ToString()
        {
            return name;
        }

        public string GetInformation()
        {
            var output = RunNetsh($"wlan show profile name=\"{Name}\" key=clear");

            if (string.IsNullOrEmpty(output))
            {
                return null;
            }
            else
            {
                return output;
            }
        }

        public string GetKeyContent()
        {
            var output = RunNetsh($"wlan show profile name=\"{Name}\" key=clear");

            if (string.IsNullOrEmpty(output))
            {
                return null;
            }
            else
            {
                // I know this is not the best way to do this, but I don't want to spend more time on this by utilizing the native WiFi API
                string[] keyContent = new[] { 
                    "Key Content", 
                    "Schlüsselinhalt", 
                    "Contenido de la clave", 
                    "Contenu de la clé", 
                    "Contenuto della chiave", 
                    "Conteúdo da chave", 
                    "Sleutelinhoud", 
                    "Nyckelinnehåll", 
                    "Nøkkelinnhold", 
                    "Avaimen sisältö",
                    "Содержимое ключа",
                    "密钥内容",
                    "キーコンテンツ",
                    "키 내용"
                };

                foreach (var key in keyContent)
                {
                    string pattern = $@"{key}\s+:\s+(.+)";
                    Match match = Regex.Match(output, pattern, RegexOptions.IgnoreCase);

                    if (match.Success)
                    {
                        return match.Groups[1].Value.Trim();
                    }
                }

                return null;
            }
        }

        private string RunNetsh(string arguments)
        {
            return Helper.RunCommand("netsh", arguments);
        }
    }
}
