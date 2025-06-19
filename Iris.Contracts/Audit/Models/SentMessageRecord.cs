using System;
namespace Iris.Contracts.Audit.Models
{
    public class SentMessageRecord
    {
        public string Address { get; set; } = "";

        public string Provider { get; set; } = "";

        public string? Message { get; set; }

        public string? User { get; set; }

        public DateTimeOffset? When { get; set; }
    }
}

