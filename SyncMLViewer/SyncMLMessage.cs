using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncMLViewer
{
    public class SyncMlMessage
    {
        public string SessionId { get; set; }
        public string MsgId { get; set; }
        public string Xml { get; set; }
        public DateTime DateTime { get; set; }
        public string Entry { get => $"{MsgId} - {DateTime}"; }

        public override string ToString()
        {
            return $"{MsgId} - {DateTime}";
        }

        public SyncMlMessage(string sessionId, string msgId, string xml)
        {
            this.SessionId = sessionId;
            this.MsgId = msgId;
            this.Xml = xml;
            this.DateTime = DateTime.Now;
        }
    }
}
