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
        private bool _inProgress;

        public event PropertyChangedEventHandler PropertyChanged;

        public SyncMlProgress()
        {
        }

        public bool InProgress
        {
            get => _inProgress;
            set
            {
                _inProgress = value;
                OnPropertyChanged("InProgress");
            }
        }

        protected void OnPropertyChanged(string inProgress)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            handler?.Invoke(this, new PropertyChangedEventArgs(inProgress));
        }
    }
}
