using System;
namespace Iris.Brokers.Models
{
    /// <summary>
    /// Base class for all connection data information
    /// </summary>
    public class ConnectionData
    {
        public string? Uri { get; set; }

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
        /// or no credentials at all when <see cref="Uri"/> names an emulator.
        /// </summary>
        public string? CredentialsPath { get; set; }

        /// <summary>
        /// Which of Google's credential sources was chosen: <c>ApplicationDefault</c>,
        /// <c>CredentialsFile</c> or <c>Emulator</c>. Blank means it is inferred from which of
        /// <see cref="Uri"/> and <see cref="CredentialsPath"/> is filled in.
        /// </summary>
        public string? CredentialSource { get; set; }

        /// <summary>
        /// How the connection authenticates. The values are provider-specific; Amazon's are
        /// in <c>AwsAuthModes</c>. Blank means the provider's original scheme.
        /// </summary>
        public string? AuthMode { get; set; }

        /// <summary>
        /// The named AWS profile credentials come from when <see cref="AuthMode"/> is Profile.
        /// </summary>
        public string? Profile { get; set; }

        public static ConnectionData FromContract(Iris.Contracts.Brokers.Models.ConnectionData data)
        {
            return new ConnectionData
            {
                Uri = data.Uri,
                Username = data.Username,
                Password = data.Password,
                ConnectionString = data.ConnectionString,
                Region = data.Region,
                VHost = data.VHost,
                ProjectId = data.ProjectId,
                CredentialsPath = data.CredentialsPath,
                CredentialSource = data.CredentialSource,
                AuthMode = data.AuthMode,
                Profile = data.Profile
            };
        }
    }
}

