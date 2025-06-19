using System;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
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
    public class AzureServiceBusContainerTests : IAsyncLifetime
    {
        private const string QueueName = "integration-test";
        private const string SqlContainerAlias = "sqledge";
        private const string ServiceBusContainerAlias = "servicebus";
        private const string SqlPassword = "YourStrongPassword123!";
        private const ushort SqlPort = 1433;
        private const ushort ServiceBusPort = 5672;

        private readonly IContainer _sqlContainer;
        private readonly IContainer _serviceBusContainer;
        private readonly string _connectionString;
        public string ConnectionString => _connectionString;

        public AzureServiceBusContainerTests()
        {
            var network = new NetworkBuilder()
                .WithName("sb-network")
                .Build();

// SQL Container
            _sqlContainer = new ContainerBuilder()
                .WithImage("mcr.microsoft.com/azure-sql-edge:latest")
                .WithName(SqlContainerAlias)
                .WithPortBinding(SqlPort, true)
                .WithEnvironment("ACCEPT_EULA", "Y")
                .WithEnvironment("MSSQL_SA_PASSWORD", SqlPassword)
                .WithNetwork(network)
                .WithNetworkAliases(SqlContainerAlias) // Alias for communication
                .Build();

// Service Bus Emulator Container
            _serviceBusContainer = new ContainerBuilder()
                .WithImage("mcr.microsoft.com/azure-messaging/servicebus-emulator:latest")
                .WithName(ServiceBusContainerAlias)
                .WithPortBinding(ServiceBusPort, true)
                .WithEnvironment("ACCEPT_EULA", "Y")
                .WithEnvironment("SQL_SERVER", SqlContainerAlias) // Alias defined in the SQL container
                .WithEnvironment("MSSQL_SA_PASSWORD", SqlPassword)
                .WithNetwork(network)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(ServiceBusPort))
                .DependsOn(_sqlContainer) // SQL container must start first
                .Build();

            _connectionString =
                "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";
        }

        public async Task InitializeAsync()
        {
            //await _sqlContainer.StartAsync();
           // await _serviceBusContainer.StartAsync();
        }

        public async Task DisposeAsync()
        {
          //  await _serviceBusContainer.StopAsync();
          //  await _sqlContainer.StopAsync();
        }

/*
        [Fact(DisplayName = "Can connect to Azure Service Bus Emulator", Timeout = 120000)]
        public async Task Can_ConnectTo_AzureServiceBusEmulator()
        {
            // Arrange
            var queueClient = new ServiceBusAdministrationClient(ConnectionString);
            await queueClient.CreateQueueAsync(QueueName);

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

        [Fact(DisplayName = "Can send message to Azure Service Bus Emulator", Timeout = 120000)]
        public async Task Can_SendMessageTo_AzureServiceBusEmulator()
        {
            // Arrange
            var queueClient = new ServiceBusAdministrationClient(ConnectionString);
            await queueClient.CreateQueueAsync(QueueName);

            var messageText = "{\"Red\": 0, \"Green\": 20, \"Blue\": 30}";

            var connection = new AzureServiceBusConnection(new()
            {
                Connector = new AzureConnector(new LoggerFactory()),
                Address = ConnectionString
            },
            new ServiceBusAdministrationClient(ConnectionString),
            new ServiceBusClient(ConnectionString)
            );

            // Act
            Func<Task> send = () => connection.SendAsync(new() { Name = QueueName, Address = ConnectionString, Provider = "Azure", Type = "Queue" }, messageText);

            // Assert
            await send.Should().NotThrowAsync();
        }
        
        */
    }
}

