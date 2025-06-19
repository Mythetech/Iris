using System;
using System.Net.Http.Json;
using FastEndpoints.Testing;
using FluentAssertions;
using Iris.Brokers;
using Iris.Contracts.Brokers.Models;
using Iris.Integration.Tests.Brokers.EmulatedProvider;
using Iris.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using static Iris.Contracts.Brokers.Endpoints.GetAllProviders;

namespace Iris.Integration.Tests.Brokers
{
    [Collection("IsolatedProvider")]
    public class GetAllProvidersTests : TestBase<IrisTestAppFixture>
    {
        private readonly HttpClient _client;
        private readonly IrisTestAppFixture _fixture;

        public GetAllProvidersTests(IrisTestAppFixture fixture)
        {
            _client = fixture.CreateClient();
            _fixture = fixture;
        }

        [Fact(DisplayName = "Get All Providers returns connections")]
        public async Task GetAllProviders_Returns_AllConnections()
        {
            // Act
            using var scope = _fixture.Services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();

            var manager = scope.ServiceProvider.GetRequiredService<IBrokerConnectionManager>();

            await manager.AddConnectionAsync(new EmulatedConnection(1));

            var response = await _client.GetFromJsonAsync<GetAllProvidersResponse>("api/providers");

            // Assert
            response.Should().NotBeNull();
            var providers = response!.Providers;
            providers.Count.Should().BeGreaterThanOrEqualTo(1);
        }
    }
}

