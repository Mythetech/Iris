using System;
using System.Text.Json;

namespace Iris.Contracts.Brokers.Models
{
    public class ConnectionData
    {
        public string Provider { get; set; } = "";

        public string Uri { get; set; } = "";

        public string? Username { get; set; }

        public string? Password { get; set; }

        public string? ConnectionString { get; set; }

        public string? Region { get; set; }

        public string ToJson()
        {
            return JsonSerializer.Serialize(this);
        }

        public static ConnectionData FromJson(string json)
        {
            return JsonSerializer.Deserialize<ConnectionData>(json) ?? throw new Exception("Unable to deserialize connection data");
        }

        public static ConnectionData FromJsonSafe(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<ConnectionData>(json) ?? new();
            }
            catch
            {
                return new();
            }
        }
    }
}

