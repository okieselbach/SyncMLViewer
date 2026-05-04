using System;
using System.Collections.Generic;

namespace SyncMLViewer
{
    /// <summary>
    /// Represents a generic ETW event captured from user-defined providers.
    /// </summary>
    public class EtwMessage
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

        public EtwMessage()
        {
            PayloadFields = new Dictionary<string, string>();
            Timestamp = DateTime.Now;
        }

        public EtwMessage(int eventId, string eventName, string providerName, Guid providerGuid)
            : this()
        {
            EventId = eventId;
            EventName = eventName;
            ProviderName = providerName;
            ProviderGuid = providerGuid;
        }

        public string GetFormattedPayload()
        {
            if (PayloadFields == null || PayloadFields.Count == 0)
            {
                return FormattedMessage ?? "(no payload data)";
            }

            var lines = new List<string>();
            lines.Add($"=== ETW Event: {EventName} (ID: {EventId}) ===");
            lines.Add($"Provider: {ProviderName}");
            lines.Add($"Provider GUID: {ProviderGuid}");
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
