using System;
using EasyNetQ.Management.Client;
using Iris.Brokers.Exceptions;
using Iris.Brokers.Models;

namespace Iris.Brokers.RabbitMQ
{
    public class RabbitMqConnector : IConnector
    {
        public string? VHost { get; set; }

        public RabbitMqConnector()
        {
        }

        public string Provider { get => "RabbitMq"; }

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

            VHost = (!string.IsNullOrWhiteSpace(data?.Username) && (!data?.Username.Equals("guest", StringComparison.OrdinalIgnoreCase) ?? true)) ? data?.Username! : "/";

            var connection = new RabbitMqConnection(new ConnectionMetadata()
            {
                Connector = this,

            }, client);

            if (discoverEndpoints && connection != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await connection.GetEndpointsAsync();
            }

            return connection;
        }
    }
}

