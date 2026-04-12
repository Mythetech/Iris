using System;
using Azure.Storage.Queues;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Iris.Brokers;
using Iris.Brokers.Azure;
using Iris.Brokers.Models;
using Microsoft.Extensions.Logging;
using Testcontainers.Azurite;

namespace Iris.Integration.Tests.Brokers
{
    [Trait("Category", "Container")]
    public class AzureStorageQueueServiceContainerTests : IAsyncLifetime
    {
        private const string QueueName = "integration-test";
        private const string AzuriteAccountName = "devstoreaccount1";
        private const string AzuriteAccountKey =
            "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

        private string ConnectionString = null!;
        private readonly AzuriteContainer _azuriteContainer;
        private readonly ILogger<AzureQueueStorageConnection> _logger;

        public AzureStorageQueueServiceContainerTests()
        {
            _logger = new LoggerFactory().CreateLogger<AzureQueueStorageConnection>();

            _azuriteContainer = new AzuriteBuilder()
                    .WithImage("mcr.microsoft.com/azure-storage/azurite")
                    .WithCommand("--skipApiVersionCheck")
                .Build();
        }

        public async Task InitializeAsync()
        {
            await _azuriteContainer.StartAsync();
            ConnectionString = _azuriteContainer.GetConnectionString();
        }

        public async Task DisposeAsync()
        {
            await _azuriteContainer.DisposeAsync();
        }

        [Fact(DisplayName = "Can connect to Azure Queue Storage")]
        public async Task Can_ConnectTo_AzureQueueStorage()
        {
            // Arrange
            var queueClient = new QueueClient(ConnectionString, QueueName);
            await queueClient.CreateIfNotExistsAsync();

            var connectionData = new ConnectionData()
            {
                ConnectionString = ConnectionString
            };

            var azureConnector = new AzureConnector(new LoggerFactory());


            // Act
            var connection = await azureConnector.ConnectAsync(connectionData, true);

            // Assert
            connection.Should().NotBeNull();
            connection!.EndpointCount.Should().Be(1);
        }

        [Fact(DisplayName = "Can send message to Azure Storage Queue")]
        public async Task Can_SendMessageTo_AzureQueueStorage()
        {
            // Arrange
            var queueClient = new QueueClient(ConnectionString, QueueName);
            await queueClient.CreateIfNotExistsAsync();

            var messageText = "{\"Red\": 0, \"Green\": 20, \"Blue\": 30}";

            var connection = new AzureQueueStorageConnection(new()
            {
                Connector = new AzureConnector(new LoggerFactory()),
                Address = ConnectionString
            },
            new QueueServiceClient(ConnectionString),
            _logger
            );

            // Act
            Func<Task> send = () => connection.SendAsync(new() { Name = QueueName, Address = ConnectionString, Provider = "Azure", Type = "Queue" }, messageText);

            // Assert
            await send.Should().NotThrowAsync();
        }

        private async Task<AzureQueueStorageConnection> CreateAndSeedAsync(string queueName, int count)
        {
            var queueClient = new QueueClient(ConnectionString, queueName);
            await queueClient.CreateIfNotExistsAsync();
            // Drain anything left from prior tests in the same fixture run.
            await queueClient.ClearMessagesAsync();

            var connection = new AzureQueueStorageConnection(
                new ConnectionMetadata
                {
                    Connector = new AzureConnector(new LoggerFactory()),
                    Address = ConnectionString
                },
                new QueueServiceClient(ConnectionString),
                _logger);

            for (var i = 0; i < count; i++)
            {
                await connection.SendAsync(
                    new EndpointDetails { Name = queueName, Address = ConnectionString, Provider = "Azure", Type = "Queue" },
                    $"{{\"index\":{i}}}");
            }

            return connection;
        }

        private EndpointDetails Endpoint(string name) => new()
        {
            Name = name,
            Address = ConnectionString,
            Provider = "Azure",
            Type = "Queue",
        };

        [Fact(DisplayName = "AzureQueueStorageConnection implements IMessagePeeker and IMessageReceiver")]
        public async Task Connection_implements_reader_interfaces()
        {
            var connection = await CreateAndSeedAsync("iris-iface-probe", 0);

            connection.Should().BeAssignableTo<IMessagePeeker>();
            connection.Should().BeAssignableTo<IMessageReceiver>();
            connection.Should().NotBeAssignableTo<IDeadLetterPeeker>();
            connection.Should().NotBeAssignableTo<IDeadLetterReceiver>();
        }

        [Fact(DisplayName = "AQS Peek returns sent messages without removing them")]
        public async Task Peek_is_non_destructive()
        {
            const string queue = "iris-aqs-peek-test";
            var connection = await CreateAndSeedAsync(queue, 3);
            var peeker = (IMessagePeeker)connection;

            var first = await peeker.PeekAsync(Endpoint(queue), 10);
            var second = await peeker.PeekAsync(Endpoint(queue), 10);

            first.Should().HaveCount(3);
            second.Should().HaveCount(3);
            first.Select(m => m.Body).Should().BeEquivalentTo(second.Select(m => m.Body));
        }

        [Fact(DisplayName = "AQS Receive removes messages")]
        public async Task Receive_is_destructive()
        {
            const string queue = "iris-aqs-receive-test";
            var connection = await CreateAndSeedAsync(queue, 3);
            var receiver = (IMessageReceiver)connection;
            var peeker = (IMessagePeeker)connection;

            var received = await receiver.ReceiveAsync(Endpoint(queue), 10);
            var afterPeek = await peeker.PeekAsync(Endpoint(queue), 10);

            received.Should().HaveCount(3);
            afterPeek.Should().BeEmpty();
        }

        [Fact(DisplayName = "AQS Received messages carry provider and metadata")]
        public async Task Receive_maps_metadata()
        {
            const string queue = "iris-aqs-metadata-test";
            var connection = await CreateAndSeedAsync(queue, 1);
            var receiver = (IMessageReceiver)connection;

            var msgs = await receiver.ReceiveAsync(Endpoint(queue), 1);

            msgs.Should().HaveCount(1);
            var m = msgs[0];
            m.Provider.Should().Be("AzureQueueStorage");
            m.Body.Should().Contain("\"index\":0");
            m.Native.Should().NotBeNull();
            m.Native!.PopReceipt.Should().NotBeNullOrWhiteSpace();
            m.DeliveryCount.Should().NotBeNull();
        }
    }
}

