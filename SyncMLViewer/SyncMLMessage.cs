using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncMLViewer
{
    class SyncMlMessage
    {
        public string SessionId { get; set; }
        public string MsgId { get; set; }
        public string Xml { get; set; }

        public override string ToString()
        {
            return MsgId.ToString();
        }

        public SyncMlMessage(string sessionId, string msgId, string xml)
        {
            this.SessionId = sessionId;
            this.MsgId = msgId;
            this.Xml = xml;
        }
    }
}
