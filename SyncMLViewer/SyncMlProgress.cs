using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SyncMLViewer
{
    public class SyncMlProgress : BindableBase
    {
        private bool _notInProgress;

        public SyncMlProgress()
        {
            _notInProgress = true;
        }

        public bool NotInProgress
        {
            get => _notInProgress;
            set
            {
                _notInProgress = value;
                OnPropertyChanged("NotInProgress");
            }
        }
    }
}
