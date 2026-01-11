using System;
using FluentAssertions;
using Iris.Brokers;
using Iris.Brokers.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Iris.Integration.Tests.Brokers
{
    public class AmazonSimpleQueueServiceTests : IClassFixture<IrisWebApplicationFactory>
    {
        private readonly IrisWebApplicationFactory _factory;

        // Example credentials - replace with your own AWS IAM credentials
        // or use environment variables for CI/CD
        private ConnectionData _data = new()
        {
            Username = "YOUR_AWS_ACCESS_KEY_ID",
            Password = "YOUR_AWS_SECRET_ACCESS_KEY",
            Region = Amazon.RegionEndpoint.USEast1.SystemName,
        };

        public AmazonSimpleQueueServiceTests(IrisWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact(DisplayName = "Can connect to amazon simple queue service and retrieve endpoints")]
        public async Task Can_ConnectTo_AmazonSimpleQueueService()
        {
            // Arrange
            var data = _data;

            var connectors = _factory.Services.GetRequiredService<IEnumerable<IConnector>>();
            var connector = connectors?.FirstOrDefault(x => x.Provider.Equals("amazon", StringComparison.OrdinalIgnoreCase));
            connector.Should().NotBeNull();

            // Act
            var connection = await connector!.ConnectAsync(data);
            var endpoints = await connection.GetEndpointsAsync();

            // Assert
            connection.Should().NotBeNull();
            connection!.EndpointCount.Should().BeGreaterThan(0);
            endpoints.Count.Should().BeGreaterThan(0);
            connection!.EndpointCount.Should().Be(endpoints.Count);
            endpoints.Any(x => x.Type.Equals("queue", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
        }
        
        [Fact(DisplayName = "Can send message to Amazon SQS")]
        public async Task Can_SendMessageTo_AmazonWebServicesQueue()
        {
            // Arrange
            var data = _data;

            var connectors = _factory.Services.GetRequiredService<IEnumerable<IConnector>>();
            var connector = connectors?.FirstOrDefault(x => x.Provider.Equals("amazon", StringComparison.OrdinalIgnoreCase));
            connector.Should().NotBeNull();

            // Act
            var connection = await connector!.ConnectAsync(data, false);

            Func<Task> act = async () =>
            {
                await connection.SendAsync(new EndpointDetails()
                {
                    Provider = "Amazon",
                    Address = "localhost/integration-test",
                    Type = "queue",
                    Name = "changecolorscommand"
                }, "{\"Red\": 0, \"Green\": 20, \"Blue\": 40}");
            };
            
            // Assert
            connection.Should().NotBeNull();
            await act.Should().NotThrowAsync();
        }
    }
}

