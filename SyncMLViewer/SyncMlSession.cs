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

        public ObservableCollection<SyncMlMessage> Messages { get; set; }

        public override string ToString()
        {
            return SessionId.ToString();
        }

        public SyncMlSession(string sessionId)
        {
            this.SessionId = sessionId;
        }
    }
}
