using System;
using Iris.Brokers;
using Microsoft.Extensions.DependencyInjection;

namespace Iris.Integration.Tests
{
    public class MassTransitTests : IClassFixture<IrisWebApplicationFactory>
    {
        private readonly IrisWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public MassTransitTests(IrisWebApplicationFactory factory)
        {
            _factory = factory;

            _client = factory.CreateClient();
        }

        public async Task Can_Consume_MassTransit_Message()
        {
            // Arrange
            var manager = _factory.Services.GetRequiredService<IBrokerConnectionManager>();

            //var azure = manager.Connectors.FirstOrDefault(x => x.Provider)

            // Act

            // Assert

        }
    }
}

