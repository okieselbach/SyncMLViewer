using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SyncMLViewer
{

    public class TraceEventSessionState : BindableBase
    {
        private bool _started;

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
                OnPropertyChanged("Started");
            }
        }
    }
}
