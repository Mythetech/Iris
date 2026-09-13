using System.Text.Json;
using FluentAssertions;
using Iris.Brokers.Frameworks;
using Iris.Brokers.Models;
using Xunit;

namespace Iris.Brokers.Test.Frameworks;

public class MassTransitAdapterTests
{
    [Fact(DisplayName = "Declares only body keys and writes nothing outside the body")]
    public void Keys_AreBodyOnly()
    {
        var adapter = new MassTransitAdapter();

        adapter.Keys.Should().NotBeEmpty();
        adapter.Keys.Should().OnlyContain(k => k.Location == KeyLocation.Body);
        FrameworkKeyAssertions.AssertKeysMatchWrite(adapter,
            MessageRequest.Create("Greeting", "{\"id\":1}", false, "MyApp.Messages.Greeting"));
    }

    [Theory(DisplayName = "Message type urn separates namespace from type name with a colon")]
    // A .NET fully qualified name, which is what the type picker and the Type name field's
    // own help text produce. Getting this wrong was the regression this test exists to pin.
    [InlineData("MyApp.Messages.Greeting", "urn:message:MyApp.Messages:Greeting")]
    // An Azure Service Bus entity name, because ServiceBusMessageNameFormatter uses '/'.
    [InlineData("MyApp.Messages/Greeting", "urn:message:MyApp.Messages:Greeting")]
    // A RabbitMQ exchange name, which is already the urn spelling.
    [InlineData("MyApp.Messages:Greeting", "urn:message:MyApp.Messages:Greeting")]
    // A bare endpoint name with no namespace is left exactly as it was given.
    [InlineData("Greeting", "urn:message:Greeting")]
    // A deeper namespace keeps every dot but the last.
    [InlineData("MyApp.Contracts.Orders.OrderPlaced", "urn:message:MyApp.Contracts.Orders:OrderPlaced")]
    public void CreateWrappedMessage_UrnSpellings(string typeName, string expectedUrn)
    {
        var request = MessageRequest.Create("Greeting", "{\"id\":1}", false, typeName);

        var body = new MassTransitAdapter().CreateWrappedMessage(request);

        MessageTypesOf(body).Should().Equal(expectedUrn);
    }

    [Fact(DisplayName = "Blank type name falls back to the endpoint name")]
    public void CreateWrappedMessage_FallsBackToEndpointName()
    {
        var request = MessageRequest.Create("iris-queue", "{\"id\":1}", false);

        var body = new MassTransitAdapter().CreateWrappedMessage(request);

        MessageTypesOf(body).Should().Equal("urn:message:iris-queue");
    }

    [Fact(DisplayName = "Each send mints its own identifiers")]
    public void CreateWrappedMessage_IdentifiersAreUniquePerSend()
    {
        var request = MessageRequest.Create("Greeting", "{\"id\":1}", false, "MyApp.Messages.Greeting");
        var adapter = new MassTransitAdapter();

        var first = JsonDocument.Parse(adapter.CreateWrappedMessage(request)).RootElement;
        var second = JsonDocument.Parse(adapter.CreateWrappedMessage(request)).RootElement;

        // Minting these once per envelope must not turn into minting them once per process.
        foreach (var property in new[] { "MessageId", "CorrelationId", "ConversationId" })
        {
            var firstValue = first.GetProperty(property).GetString();
            firstValue.Should().NotBeNullOrWhiteSpace(property);
            firstValue.Should().NotBe(second.GetProperty(property).GetString(), property);
        }
    }

    [Fact(DisplayName = "RequestId and InitiatorId are null so consumers do not treat the send as a request")]
    public void CreateWrappedMessage_NoRequestSemantics()
    {
        var request = MessageRequest.Create("Greeting", "{\"id\":1}", false, "MyApp.Messages.Greeting");

        var body = JsonDocument.Parse(new MassTransitAdapter().CreateWrappedMessage(request)).RootElement;

        body.GetProperty("RequestId").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("InitiatorId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact(DisplayName = "SourceAddress is an absolute endpoint uri and SentTime is UTC")]
    public void CreateWrappedMessage_AddressAndTime()
    {
        var request = MessageRequest.Create("Greeting", "{\"id\":1}", false, "MyApp.Messages.Greeting");

        var body = JsonDocument.Parse(new MassTransitAdapter().CreateWrappedMessage(request)).RootElement;

        var sourceAddress = body.GetProperty("SourceAddress").GetString();
        Uri.TryCreate(sourceAddress, UriKind.Absolute, out _).Should().BeTrue(sourceAddress);

        var sentTime = body.GetProperty("SentTime").GetDateTime();
        sentTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    private static string[] MessageTypesOf(string body) =>
        JsonDocument.Parse(body).RootElement
            .GetProperty("MessageType")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToArray();
}
