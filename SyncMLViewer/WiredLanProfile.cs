using System;

namespace SyncMLViewer
{
    public enum WiredLanProfileLocation
    {
        Machine,    // Stored in \Machine folder (e.g., from Intune/MDM) - source profile
        Interface   // Stored in \Interfaces\{GUID} folder - applied instance
    }

    public class WiredLanProfile : BindableBase
    {
        private string _name;
        private string _xml;
        private string _interfaceGuid;
        private string _interfaceName;
        private string _filePath;
        private WiredLanProfileLocation _location;
        private bool _isDeletable;

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

        /// <summary>
        /// The interface GUID (for interface-based profiles)
        /// </summary>
        public string InterfaceGuid
        {
            get => _interfaceGuid;
            set
            {
                _interfaceGuid = value;
                OnPropertyChanged("InterfaceGuid");
            }
        }

        /// <summary>
        /// The friendly interface name (e.g., "Ethernet", "Ethernet 2")
        /// </summary>
        public string InterfaceName
        {
            get => _interfaceName;
            set
            {
                _interfaceName = value;
                OnPropertyChanged("InterfaceName");
            }
        }

        /// <summary>
        /// The full file path to the profile XML
        /// </summary>
        public string FilePath
        {
            get => _filePath;
            set
            {
                _filePath = value;
                OnPropertyChanged("FilePath");
            }
        }

        /// <summary>
        /// Where the profile is stored (Machine or Interface folder)
        /// </summary>
        public WiredLanProfileLocation Location
        {
            get => _location;
            set
            {
                _location = value;
                OnPropertyChanged("Location");
            }
        }

        /// <summary>
        /// Whether this profile can be deleted via netsh (allows attempt)
        /// </summary>
        public bool IsDeletable
        {
            get => _isDeletable;
            set
            {
                _isDeletable = value;
                OnPropertyChanged("IsDeletable");
            }
        }

        /// <summary>
        /// Display string showing location info
        /// </summary>
        public string LocationDisplay
        {
            get
            {
                if (Location == WiredLanProfileLocation.Machine)
                {
                    return "Machine";
                }
                else
                {
                    return string.IsNullOrEmpty(InterfaceName) ? $"Interface" : InterfaceName;
                }
            }
        }

        public WiredLanProfile(string name, string xml)
        {
            Name = name;
            Xml = xml;
            Location = WiredLanProfileLocation.Machine;
            IsDeletable = true;
        }

        public WiredLanProfile(string name, string xml, WiredLanProfileLocation location, string filePath, string interfaceGuid = null, string interfaceName = null, bool isDeletable = true)
        {
            Name = name;
            Xml = xml;
            Location = location;
            FilePath = filePath;
            InterfaceGuid = interfaceGuid;
            InterfaceName = interfaceName;
            IsDeletable = isDeletable;
        }

        public override string ToString()
        {
            if (Location == WiredLanProfileLocation.Machine)
            {
                return $"{Name} [Machine]";
            }
            else
            {
                var ifName = string.IsNullOrEmpty(InterfaceName) ? "Interface" : InterfaceName;
                return $"{Name} [{ifName}]";
            }
        }
    }
}
