using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Azure.Storage.Queues;
using Iris.Brokers.Exceptions;
using Iris.Brokers.Models;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.Extensions.Logging;

namespace Iris.Brokers.Azure
{
    public class AzureConnector : IConnector
    {
        private ILoggerFactory _loggerFactory;
        private ILogger<AzureConnector> _logger;    
        public AzureConnector(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<AzureConnector>();
        }

        public string Provider
        {
            get => "Azure";
        }
        
        public Task<IConnection?> ConnectAsync(ConnectionData data, bool discoverEndpoints = true)
            => ConnectAsync(data, CancellationToken.None, discoverEndpoints);

        public async Task<IConnection?> ConnectAsync(ConnectionData data, CancellationToken cancellationToken, bool discoverEndpoints = true)
        {
            if (string.IsNullOrWhiteSpace(data.ConnectionString))
            {
                throw new InvalidConnectionException("Connection string empty");
            }

            _logger.LogInformation($"Connection string passed basic validation");

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (data.ConnectionString.Contains("Endpoint=sb") ||
                    (data?.ConnectionString?.Contains("servicebus", StringComparison.OrdinalIgnoreCase) ?? false))
                {
                    _logger.LogInformation("Determined connection string is azure service bus, connecting...");
                    return await ConnectToAzureServiceBusAsync(data.ConnectionString, cancellationToken, discoverEndpoints);
                }
                else
                {
                    _logger.LogInformation("Determined connection string is azure queue storage, connecting...");
                    return await ConnectToAzureQueueStorageAsync(data!.ConnectionString!, cancellationToken, discoverEndpoints);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw new InvalidConnectionException("Failed to create an azure connection with given connection string", ex);
            }
        }

        private async Task<IConnection?> ConnectToAzureServiceBusAsync(string connectionString, CancellationToken cancellationToken, bool discoverEndpoints = true)
        {
            var client = new ServiceBusClient(connectionString);

            var adminClient = new ServiceBusAdministrationClient(connectionString);

            var connection = new AzureServiceBusConnection(new ConnectionMetadata()
            {
                Connector = this,
                Address = client.FullyQualifiedNamespace,
            },
            adminClient,
            client);

            if (discoverEndpoints && connection != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var endpoints = await connection.GetEndpointsAsync();
            }

            return connection;
        }

        private async Task<IConnection?> ConnectToAzureQueueStorageAsync(string connectionString, CancellationToken cancellationToken, bool discoverEndpoints = true)
        {
            var serviceClient = new QueueServiceClient(connectionString, new QueueClientOptions()
            {
                MessageEncoding = QueueMessageEncoding.Base64
            });

            var connection = new AzureQueueStorageConnection(new ConnectionMetadata()
            {
                Connector = this,
                Address = serviceClient.Uri.GetLeftPart(UriPartial.Authority)
            },
            serviceClient,
            _loggerFactory.CreateLogger<AzureQueueStorageConnection>()
            );

            if (discoverEndpoints && connection != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _ = await connection.GetEndpointsAsync();
            }

            return connection;
        }
    }
}
