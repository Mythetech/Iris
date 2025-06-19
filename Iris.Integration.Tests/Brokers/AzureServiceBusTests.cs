using System;
using FluentAssertions;
using Iris.Brokers;
using Iris.Brokers.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Iris.Integration.Tests.Brokers
{
    public class AzureServiceBusTests : IClassFixture<IrisWebApplicationFactory>
    {
        private readonly IrisWebApplicationFactory _factory;

        private ConnectionData _data = new()
        {
            ConnectionString = "Endpoint=sb://iris-cloud.servicebus.windows.net/;SharedAccessKeyName=integration-test;SharedAccessKey=Q5SvdXPe2EPbTk8X9AGq0Xry2azzoEpzK+ASbMFtn7E="
        };

        public AzureServiceBusTests(IrisWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact(DisplayName = "Can connect to azure service bus and retrieve endpoints")]
        public async Task Can_ConnectTo_AzureServiceBus()
        {
            // Arrange
            var data = _data;

            var connectors = _factory.Services.GetRequiredService<IEnumerable<IConnector>>();
            var connector = connectors?.FirstOrDefault(x => x.Provider.Equals("azure", StringComparison.OrdinalIgnoreCase));
            connector.Should().NotBeNull();

            // Act
            var connection = await connector!.ConnectAsync(data);
            var endpoints = await connection.GetEndpointsAsync();

            // Assert
            connection.Should().NotBeNull();
            data.ConnectionString.Should().Contain(connection.Address);
            connection!.EndpointCount.Should().BeGreaterThan(0);
            endpoints.Count.Should().BeGreaterThan(0);
            connection!.EndpointCount.Should().Be(endpoints.Count);
            endpoints.Any(x => x.Type.Equals("queue", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
        }


        [Fact(DisplayName = "Can send message to Azure Service Bus Queue")]
        public async Task Can_SendMessageTo_AzureServiceBusQueue()
        {
            // Arrange
            var data = _data;

            var connectors = _factory.Services.GetRequiredService<IEnumerable<IConnector>>();
            var connector = connectors?.FirstOrDefault(x => x.Provider.Equals("azure", StringComparison.OrdinalIgnoreCase));
            connector.Should().NotBeNull();

            // Act
            var connection = await connector!.ConnectAsync(data, false);

            Func<Task> act = async () =>
            {
                await connection.SendAsync(new EndpointDetails()
                {
                    Provider = "azureservicebus",
                    Address = connection.Address,
                    Type = "queue",
                    Name = "integration-test"
                }, "{\"Red\": 0, \"Green\": 20, \"Blue\": 40}");
            };
            
            // Assert
            connection.Should().NotBeNull();
            await act.Should().NotThrowAsync();
        }
    }
}
