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

        public static ConnectionData FromContract(Iris.Contracts.Brokers.Models.ConnectionData data)
        {
            return new ConnectionData
            {
                Uri = data.Uri,
                Username = data.Username,
                Password = data.Password,
                ConnectionString = data.ConnectionString,
                Region = data.Region,
                VHost = data.VHost
            };
        }
    }
}

