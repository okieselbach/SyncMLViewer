using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncMLViewer
{
    public class VpnProfile : BindableBase
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

        public VpnProfile(string name, string xml)
        {
            Name = name;
            Xml = xml;
        }

        public override string ToString()
        {
            return name;
        }
    }
}
