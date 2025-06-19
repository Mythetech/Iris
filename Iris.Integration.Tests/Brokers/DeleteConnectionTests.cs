using System;
using FastEndpoints.Testing;
using FluentAssertions;
using Iris.Brokers;
using Iris.Integration.Tests.Brokers.EmulatedProvider;
using Iris.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using static Iris.Contracts.Brokers.Endpoints.DeleteConnection;
using Iris.Api.Services.Integrations.Domain;
using MassTransit.Mediator;
using Iris.Contracts.Integrations;
using System.Text.Json;
using Iris.Brokers.Models;

namespace Iris.Integration.Tests.Brokers
{

    [Collection("Iris")]
    public class DeleteConnectionTests : TestBase<IrisTestAppFixture>
    {
        private readonly HttpClient _client;
        private readonly IrisTestAppFixture _fixture;

        public DeleteConnectionTests(IrisTestAppFixture fixture)
        {
            _client = fixture.CreateClient();
            _fixture = fixture;
        }

        [Fact(DisplayName = "Delete Connection returns successful response when connection exists")]
        public async Task DeleteConnection_Returns_Successful_Response_When_Connection_Exists()
        {
            // Arrange
            using var scope = _fixture.Services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
            var manager = scope.ServiceProvider.GetRequiredService<IBrokerConnectionManager>();
            var emulatedConnection = new EmulatedConnection(1, "fakeaddress");
            await manager.AddConnectionAsync(emulatedConnection);
            var iService = _fixture.Services.GetRequiredService<IIntegrationsService>();
            var mediator = scope.ServiceProvider.GetRequiredService<IScopedMediator>();
            await mediator.Publish<ICreateIntegration>(new
            {
                Address = "fakeaddress",
                Provider = "Mock",
                Data = JsonSerializer.Serialize(new Contracts.Brokers.Models.ConnectionData() { ConnectionString = "" }),
            }, CancellationToken.None);

            var integrations = await iService.GetIntegrationsAsync();
            integrations.Where(x => x.Address.Equals("fakeaddress")).Count().Should().Be(1);

            // Act
            var response = await _client.DeleteAsJsonAsync($"api/brokers", new DeleteConnectionRequest("fakeaddress"));

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            using var scope2 = _fixture.Services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
            var integrations2 = await _fixture.Services.GetRequiredService<IIntegrationsService>().GetIntegrationsAsync();
            integrations2.Where(x => x.Address.Equals("fakeaddress")).Count().Should().Be(0);


        }

        [Fact(DisplayName = "Delete Connection returns not found when connection does not exist")]
        public async Task DeleteConnection_Returns_Not_Found_When_Connection_Does_Not_Exist()
        {
            // Act
            var response = await _client.DeleteAsync("api/brokers/nonexistentid");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }

}

