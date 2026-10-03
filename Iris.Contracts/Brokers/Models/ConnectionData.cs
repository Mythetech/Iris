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
        /// How the connection authenticates. The values are provider-specific: Amazon's are in
        /// <c>AwsAuthModes</c>; Google's are <c>ApplicationDefault</c>, <c>CredentialsFile</c>
        /// and <c>Emulator</c>. The mode is stated rather than guessed from which fields are
        /// filled in, so that, for example, a Google emulator host left blank is an error and
        /// never a connection to a real project. Blank, as on connections saved before a
        /// provider had modes, means access keys for Amazon and is inferred from the other
        /// fields for Google.
        /// </summary>
        public string? AuthMode { get; set; }

        /// <summary>
        /// The named AWS profile credentials come from when <see cref="AuthMode"/> is Profile.
        /// </summary>
        public string? Profile { get; set; }

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

