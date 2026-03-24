using System;
using System.Collections.Generic;

namespace SyncMLViewer
{
    /// <summary>
    /// Represents a Windows Notification Service (WNS) message captured via ETW.
    /// </summary>
    public class WnsMessage
    {
        public int EventId { get; set; }
        public string EventName { get; set; }
        public string ProviderName { get; set; }
        public Guid ProviderGuid { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, string> PayloadFields { get; set; }
        public string FormattedMessage { get; set; }

        public string Entry => $"[{Timestamp:HH:mm:ss.fff}] {ProviderName}: {EventName}";

        public override string ToString()
        {
            return Entry;
        }

        public WnsMessage()
        {
            PayloadFields = new Dictionary<string, string>();
            Timestamp = DateTime.Now;
        }

        public WnsMessage(int eventId, string eventName, string providerName, Guid providerGuid)
            : this()
        {
            EventId = eventId;
            EventName = eventName;
            ProviderName = providerName;
            ProviderGuid = providerGuid;
        }

        /// <summary>
        /// Gets a formatted string representation of all payload fields.
        /// </summary>
        public string GetFormattedPayload()
        {
            if (PayloadFields == null || PayloadFields.Count == 0)
            {
                return FormattedMessage ?? "(no payload data)";
            }

            var lines = new List<string>();
            lines.Add($"=== WNS Event: {EventName} (ID: {EventId}) ===");
            lines.Add($"Provider: {ProviderName}");
            lines.Add($"Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
            lines.Add("");
            lines.Add("--- Payload Fields ---");

            foreach (var kvp in PayloadFields)
            {
                lines.Add($"{kvp.Key}: {kvp.Value}");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }
}
