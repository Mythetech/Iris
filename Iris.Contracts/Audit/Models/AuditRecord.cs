using System;
namespace Iris.Contracts.Audit.Models
{
    public class AuditRecord
    {
        public required string Action { get; set; }

        public required string Target { get; set; }

        public Dictionary<string, string>? Details { get; set; }

        public string? User { get; set; }

        public DateTimeOffset? When { get; set; }

        public SentMessageRecord MapToSentMessage()
        {
            Details ??= new();

            return new SentMessageRecord
            {
                Address = Target,
                Provider = Details.ContainsKey("Provider") ? Details?["Provider"] ?? "Unknown" : "",
                Message = Details!.TryGetValue("Message", out string? value) ? value : "",
                User = User,
                When = When
            };
        }
    }

}

