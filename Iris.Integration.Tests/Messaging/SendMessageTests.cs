using System.Net;
using System.Net.Http.Json;
using FastEndpoints.Testing;
using FluentAssertions;
using Iris.Brokers;
using Iris.Brokers.Models;
using Iris.Contracts.Messaging;
using Iris.Contracts.Messaging.Models;
using Iris.Cloud.Demo.Contracts;
using Iris.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Iris.Integration.Tests.Messaging;

public class SendMessageTests : TestBase<IrisTestAppFixture>
{
    private readonly IrisTestAppFixture _fixture;
    private readonly HttpClient _client;

    public SendMessageTests(IrisTestAppFixture fixture)
    {
        _fixture = fixture;
        _client = _fixture.Client;
    }
    
    [Fact(DisplayName = "Can send a simple message")]
    public async Task Can_Send_SimpleMessage()
    {
        // Act
        var message = new Message()
        {
            Address = "fakeaddress-",
            MessageType = "ChangeColorsCommand",
            Data = "{}",
            Headers = new Dictionary<string, string>(),
            Properties = new Dictionary<string, string>(),
        };
        
        var response = await _client.PostAsJsonAsync("api/messaging/send",
            new SendMessage.SendMessageRequest(message), CancellationToken.None);

        var data = await response.Content.ReadFromJsonAsync<SendMessage.SendMessageResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        data.Success.Should().BeTrue();
    }

    [Fact(DisplayName = "Message requires an address")]
    public async Task Cannot_Send_WithoutAddress()
    {
        // Act
        var message = new Message()
        {
            Address = "",
            MessageType = "ChangeColorsCommand",
            Data = "{}",
            Headers = new Dictionary<string, string>(),
            Properties = new Dictionary<string, string>(),
        };

        var response = await _client.PostAsJsonAsync("api/messaging/send",
            new SendMessage.SendMessageRequest(message), CancellationToken.None);

        var data = await response.Content.ReadFromJsonAsync<FastEndpoints.ErrorResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        data.Errors.Should().HaveCount(1);
        data.Errors.First().Value.Should().Contain("Connection invalid");
    }


    [Fact(DisplayName = "Message requires a Message Type")]
        public async Task Cannot_Send_WithoutMessageType()
        {
            // Act
            var message = new Message()
            {
                Address = "fakeaddress-",
                MessageType = "",
                Data = "{}",
                Headers = new Dictionary<string, string>(),
                Properties = new Dictionary<string, string>(),
            };
        
            var response = await _client.PostAsJsonAsync("api/messaging/send",
                new SendMessage.SendMessageRequest(message), CancellationToken.None);

            var data = await response.Content.ReadFromJsonAsync<FastEndpoints.ErrorResponse>();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            data.Errors.Should().HaveCount(1);
            data.Errors.First().Value[0].Should().StartWith("Message Type is required");
        }
    
    [Fact(DisplayName = "Can send a simple message to Azure Service Bus")]
    public async Task Can_Send_SimpleMessage_To_AzureServiceBus()
    {
        // Arrange
        ConnectionData _data = new()
        {
            ConnectionString = "Endpoint=sb://iris-cloud.servicebus.windows.net/;SharedAccessKeyName=integration-test;SharedAccessKey=Q5SvdXPe2EPbTk8X9AGq0Xry2azzoEpzK+ASbMFtn7E="
        };

        var cm = _fixture.Services.GetRequiredService<IBrokerConnectionManager>();
        var azure = cm.GetProviders().FirstOrDefault(x => x.Provider.Equals("azure", StringComparison.OrdinalIgnoreCase));
        var connection = await azure.ConnectAsync(_data);
        await cm.AddConnectionAsync(connection);
        
        // Act
        var message = new Message()
        {
            Address = "iris-cloud.servicebus.windows.net",
            MessageType = "changecolorscommand",
            Data = "{\n    \"Red\": 0,\n    \"Green\": 0,\n    \"Blue\": 0\n}",
            Headers = new Dictionary<string, string>(),
            Properties = new Dictionary<string, string>(),
        };
        
        var response = await _client.PostAsJsonAsync("api/messaging/send",
            new SendMessage.SendMessageRequest(message), CancellationToken.None);

        var data = await response.Content.ReadFromJsonAsync<SendMessage.SendMessageResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        data.Success.Should().BeTrue();
    }
}