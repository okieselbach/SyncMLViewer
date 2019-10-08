using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PowerShell.Commands;

namespace SyncMLViewer
{
    public class SyncMlSession
    {
        public string SessionId { get; set; }

        public DateTime DateTime { get; set; }

        public string Entry => $"{SessionId} - {DateTime}";

        public override string ToString()
        {
            return $"{SessionId} - {DateTime}";
        }

        public ObservableCollection<SyncMlMessage> Messages { get; set; }

        public SyncMlSession(string sessionId)
        {
            this.SessionId = sessionId;
            this.Messages = new ObservableCollection<SyncMlMessage>();
            this.DateTime = DateTime.Now;
        }
    }
}
