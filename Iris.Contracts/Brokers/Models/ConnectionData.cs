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

        /// <summary>
        /// RabbitMQ's virtual host. Blank means the broker's default, "/".
        /// </summary>
        public string? VHost { get; set; }

        /// <summary>
        /// The Google Cloud project that owns the topics and subscriptions.
        /// </summary>
        public string? ProjectId { get; set; }

        /// <summary>
        /// Path to a Google credentials JSON file. Blank means Application Default Credentials,
        /// or no credentials at all when <see cref="Uri"/> names an emulator. A path is stored
        /// rather than the file's contents so that no key is ever written to Iris's database.
        /// </summary>
        public string? CredentialsPath { get; set; }

        /// <summary>
        /// Which of Google's credential sources was chosen: <c>ApplicationDefault</c>,
        /// <c>CredentialsFile</c> or <c>Emulator</c>. Stated rather than left to be guessed from
        /// which field is filled in, so that an emulator host left blank is an error and never a
        /// connection to a real project.
        /// </summary>
        public string? CredentialSource { get; set; }

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

