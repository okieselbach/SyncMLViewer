using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncMLViewer
{
    public class SyncMlProgress : INotifyPropertyChanged
    {
        private bool _notInProgress;

        public event PropertyChangedEventHandler PropertyChanged;

        public SyncMlProgress()
        {
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

        protected void OnPropertyChanged(string inProgress)
        {
            var handler = PropertyChanged;
            handler?.Invoke(this, new PropertyChangedEventArgs(inProgress));
        }
    }
}
