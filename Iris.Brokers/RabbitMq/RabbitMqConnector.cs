using System;
using EasyNetQ.Management.Client;
using Iris.Brokers.Exceptions;
using Iris.Brokers.Models;

namespace Iris.Brokers.RabbitMQ
{
    public class RabbitMqConnector : IConnector
    {
        /// <summary>RabbitMQ's own default virtual host, and the one a broker always has.</summary>
        public const string DefaultVHost = "/";

        public RabbitMqConnector()
        {
        }

        public string Provider { get => ConnectorProviders.RabbitMq; }

        private ManagementClient CreateClient(ConnectionData data)
        {
            if (string.IsNullOrWhiteSpace(data.ConnectionString) && string.IsNullOrWhiteSpace(data.Uri))
            {
                throw new InvalidConnectionException("Connection string or uri required");
            }
            
            if (!string.IsNullOrWhiteSpace(data.ConnectionString))
            {
                return new ManagementClient(new Uri(data.ConnectionString), data?.Username ?? "", data?.Password ?? "");
            }
            else
            {
                return new ManagementClient(new Uri(data.Uri ?? ""), username: data.Username ?? "", password: data.Password ?? "");
            }
        }


        public Task<IConnection?> ConnectAsync(ConnectionData data, bool discoverEndpoints = true)
            => ConnectAsync(data, CancellationToken.None, discoverEndpoints);

        public async Task<IConnection?> ConnectAsync(ConnectionData data, CancellationToken cancellationToken, bool discoverEndpoints = true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var client = CreateClient(data);

            // The vhost belongs to the connection, not to the connector. The connector is a DI
            // singleton, so a second RabbitMQ broker used to silently repoint the first
            // connection's sends, reads and inspections at its own vhost.
            //
            // It is also asked for rather than guessed. The old rule was "the username, unless
            // it is guest, in which case /", which meant a self-hosted broker with user admin on
            // the default vhost issued GetVhostAsync("admin") and PublishAsync("admin", ...),
            // both 404. Tests only ever used guest, so the guess always happened to be right.
            var vhost = string.IsNullOrWhiteSpace(data?.VHost) ? DefaultVHost : data.VHost.Trim();

            var connection = new RabbitMqConnection(new ConnectionMetadata()
            {
                Connector = this,

            }, client, vhost);

            if (discoverEndpoints && connection != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await connection.GetEndpointsAsync();
            }

            return connection;
        }
    }
}

