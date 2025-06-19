using System;
using FastEndpoints.Testing;
using Iris.Integration.Tests.Infrastructure;
using static Iris.Contracts.Brokers.Endpoints.CreateConnection;
using System.Text;
using System.Net.Http.Json;
using Iris.Contracts.Brokers.Models;
using FluentAssertions;
using System.Text.Json;
using Newtonsoft.Json;
using System.Net;
using FastEndpoints;
using Microsoft.Extensions.DependencyInjection;
using MassTransit.Testing;
using Iris.Brokers;
using Iris.Integration.Tests.Brokers.EmulatedProvider;

namespace Iris.Integration.Tests.Brokers
{
    [Collection("Iris")]
    public class CreateConnectionTests : TestBase<IrisTestAppFixture>
    {
        private readonly HttpClient _client;
        private readonly IrisTestAppFixture _fixture;
        private readonly ITestHarness _testHarness;
        private static readonly JsonSerializerOptions _options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };


        public CreateConnectionTests(IrisTestAppFixture fixture)
        {
            _client = fixture.CreateClient();
            _fixture = fixture;
            _testHarness = fixture.Services.GetRequiredService<ITestHarness>();
        }

        [Fact(DisplayName = "Create Connection returns successful response")]
        public async Task CreateConnection_Returns_Successful_Response()
        {
            // Arrange
            var request = new CreateConnectionRequest(
                new()
                {
                    Provider = "Mock",
                    ConnectionString = "testconnectionstring",
                },
                true,
                false
            );

            // Act
            var r = await _client.PostAsJsonAsync("api/brokers", request);
            var response = await System.Text.Json.JsonSerializer.DeserializeAsync<CreateConnectionResponse>(await r.Content.ReadAsStreamAsync(), _options);

            // Assert
            response.Should().NotBeNull();
            response.Success.Should().BeTrue();
            response.Address.Should().Contain("fakeaddress");
            response.Endpoints.Should().NotBeEmpty();
        }

        [Fact(DisplayName = "Create Connection with invalid provider returns validation error")]
        public async Task CreateConnection_With_Invalid_Provider_Returns_Validation_Error()
        {
            // Arrange
            var request = new CreateConnectionRequest(
                new()
                {
                    Provider = "FakeProvider",
                    ConnectionString = "faketestconnectionstring",
                },
                true,
                false
            );
            var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("api/brokers", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var responseContent = await response.Content.ReadAsStringAsync();
            var errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(responseContent);

            errorResponse.Should().NotBeNull();
            errorResponse.Errors.Should().ContainKey("data.Provider");
            errorResponse.Errors["data.Provider"].Should().Contain("Invalid connection provider");

        }

        [Theory(DisplayName = "Create Connection with max connections returns error")]
        [InlineData(5)]
        public async Task CreateConnection_With_Max_Connections_Returns_Error(int maxConnections)
        {
            // Arrange
            using var scope = _fixture.Services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
            var manager = scope.ServiceProvider.GetRequiredService<IBrokerConnectionManager>();
            for (int i = 0; i < maxConnections; i++)
            {
                var emulatedConnection = new EmulatedConnection(i + 1, $"TestAddress{i}");
                await manager.AddConnectionAsync(emulatedConnection);
            }

            var request = CreateTestRequest();
            var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("api/brokers", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var errorResponse = await System.Text.Json.JsonSerializer.DeserializeAsync<ErrorResponse>(await response.Content.ReadAsStreamAsync(), _options);
            errorResponse.Should().NotBeNull();
            errorResponse.Errors.Should().ContainKey("generalErrors");
            errorResponse.Errors["generalErrors"].Should().Contain($"Exceeded max connections ({maxConnections})");
            
            for (int i = 0; i < maxConnections; i++)
            {
                await manager.RemoveConnectionAsync($"TestAddress{i}");
            }
        }

        [Fact(DisplayName = "Create Connection with invalid connection information returns error")]
        public async Task CreateConnection_With_Invalid_Connection_Information_Returns_Error()
        {
            // Arrange
            var request = new CreateConnectionRequest(new ConnectionData() { Provider = "Mock", ConnectionString = null, Uri = null }, false, false);
            var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("api/brokers", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var errorResponse = await System.Text.Json.JsonSerializer.DeserializeAsync<ErrorResponse>(await response.Content.ReadAsStreamAsync(), _options);
            errorResponse.Should().NotBeNull();
            errorResponse.Errors.Should().ContainKey("data");
            errorResponse.Errors["data"].Should().Contain("Invalid connection information");
        }

        [Fact(DisplayName = "Create Connection with duplicate connection returns error")]
        public async Task CreateConnection_With_Duplicate_Connection_Returns_Error()
        {
            // Arrange
            using var scope = _fixture.Services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
            var manager = scope.ServiceProvider.GetRequiredService<IBrokerConnectionManager>();
            var emulatedConnection = new EmulatedConnection(1, "fakeaddress-testconnectionstring");
            await manager.AddConnectionAsync(emulatedConnection);

            var request = CreateTestRequest();
            var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("api/brokers", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var errorResponse = await System.Text.Json.JsonSerializer.DeserializeAsync<ErrorResponse>(await response.Content.ReadAsStreamAsync(), _options);
            errorResponse.Should().NotBeNull();
            errorResponse.Errors.Should().ContainKey("data");
            errorResponse.Errors["data"].Should().Contain("Duplicate connection");
        }
        
        [Theory(DisplayName = "Create Connection with invalid connection returns error")]
        [InlineData("RabbitMq")]
        [InlineData("Azure")]
        [InlineData("Amazon")]
        public async Task CreateConnection_With_Invalid_Connection_Returns_Error(string provider)
        {
            // Arrange
            var request = new CreateConnectionRequest(new ConnectionData() { Provider = provider, ConnectionString = null, Uri = "" }, false, false);
            var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("api/brokers", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var errorResponse = await System.Text.Json.JsonSerializer.DeserializeAsync<ErrorResponse>(await response.Content.ReadAsStreamAsync(), _options);
            errorResponse.Should().NotBeNull();
            errorResponse.Errors.Should().ContainKey("data");
            errorResponse.Errors["data"].Should().Contain("Invalid connection information");
        }

        private CreateConnectionRequest CreateTestRequest(bool save = false)
        {
            return new CreateConnectionRequest(

                new ConnectionData
                {
                    Provider = "Mock",
                    ConnectionString = "testconnectionstring",
                },
                true,
                save
            );
        }
    }
}

