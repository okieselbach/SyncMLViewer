using System;

namespace SyncMLViewer
{
    public class WiredLanProfile : BindableBase
    {
        private string _name;
        private string _xml;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged("Name");
            }
        }

        public string Xml
        {
            get => _xml;
            set
            {
                _xml = value;
                OnPropertyChanged("Xml");
            }
        }

        public WiredLanProfile(string name, string xml)
        {
            Name = name;
            Xml = xml;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
