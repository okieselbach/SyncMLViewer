using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SyncMLViewer
{

    public class TraceEventSessionState : INotifyPropertyChanged
    {
        private bool _started;

        public event PropertyChangedEventHandler PropertyChanged;

        public TraceEventSessionState()
        {
            _started = true;
        }

        public bool Started
        {
            get => _started;
            set
            {
                _started = value;
                OnPropertyChanged();
            }
        }

        protected void OnPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
