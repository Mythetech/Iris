using System;
using Azure.Storage.Queues;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Iris.Brokers.Azure;
using Iris.Brokers.Models;
using Microsoft.Extensions.Logging;
using Testcontainers.Azurite;

namespace Iris.Integration.Tests.Brokers
{
    public class AzureStorageQueueServiceContainerTests : IAsyncLifetime
    {
        private const string QueueName = "integration-test";
        private readonly string ConnectionString = "UseDevelopmentStorage=true";
        private readonly DockerContainer _azuriteContainer;
        private readonly ILogger<AzureQueueStorageConnection> _logger;

        public AzureStorageQueueServiceContainerTests()
        {
            _logger = new LoggerFactory().CreateLogger<AzureQueueStorageConnection>();

            _azuriteContainer = new AzuriteBuilder()
                    .WithImage("mcr.microsoft.com/azure-storage/azurite")
                    .WithCommand("--skipApiVersionCheck")
                    .WithPortBinding(10000)
                    .WithExposedPort(10000)
                    .WithPortBinding(10001)
                    .WithExposedPort(10001)
                .Build();
        }

        public Task InitializeAsync()
        {
            return _azuriteContainer.StartAsync();
        }

        public Task DisposeAsync()
        {
            return _azuriteContainer.DisposeAsync().AsTask();
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
    }
}

