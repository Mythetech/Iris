using System;
using FastEndpoints.Testing;
using Iris.Integration.Tests.Infrastructure;

namespace Iris.Integration.Tests.Brokers
{

    [Collection("Iris")]
    public class GetSupportedProvidersTests : TestBase<IrisTestAppFixture>
    {
        private readonly HttpClient _client;

        public GetSupportedProvidersTests(IrisTestAppFixture fixture)
        {
            _client = fixture.CreateClient();
        }

        [Fact(DisplayName = "Can call get supported providers successfully")]
        public async Task GetSupportedProviders_Returns_Successful_Response()
        {
            // Act
            var response = await _client.GetAsync("api/providers/supported");

            // Assert
            response.EnsureSuccessStatusCode();
        }
    }
}

