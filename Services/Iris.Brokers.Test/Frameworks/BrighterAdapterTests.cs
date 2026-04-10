using FluentAssertions;
using Iris.Brokers.Extensions;
using Iris.Brokers.Frameworks;
using Iris.Brokers.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Iris.Brokers.Test.Frameworks;

public class BrighterAdapterTests
{
    private const string SampleJson = "{\"orderId\":42,\"amount\":9.99}";

    private static MessageRequest NewRequest(
        string? fullyQualifiedName = "MyApp.Messages.OrderPlaced",
        string? assemblyName = null,
        Dictionary<string, string>? headers = null,
        Dictionary<string, string>? properties = null) =>
        new()
        {
            MessageType = "OrderPlaced",
            MessageFullyQualifiedName = fullyQualifiedName,
            MessageAssemblyName = assemblyName,
            Json = SampleJson,
            Framework = "Brighter",
            Headers = headers ?? new Dictionary<string, string>(),
            Properties = properties ?? new Dictionary<string, string>(),
        };

    [Fact(DisplayName = "CreateWrappedMessage returns the POCO body unchanged")]
    public void CreateWrappedMessage_ReturnsBodyUnchanged()
    {
        var adapter = new BrighterAdapter();
        var request = NewRequest();

        var result = adapter.CreateWrappedMessage(request);

        result.Should().Be(SampleJson);
    }

    [Fact(DisplayName = "Sets the CloudEvents 1.0 envelope headers Brighter v10 expects")]
    public void CreateWrappedMessage_SetsCloudEventsHeaders()
    {
        var adapter = new BrighterAdapter();
        var request = NewRequest();

        adapter.CreateWrappedMessage(request);

        request.Headers["cloudEvents_specversion"].Should().Be("1.0");
        request.Headers["cloudEvents_type"].Should().Be("MyApp.Messages.OrderPlaced");
        request.Headers["cloudEvents_source"].Should().Be("iris://broker");
        request.Headers.Should().ContainKey("cloudEvents_id");
        request.Headers["cloudEvents_id"].Should().MatchRegex(@"^[0-9a-fA-F-]{36}$");
        request.Headers.Should().ContainKey("cloudEvents_time");
    }

    [Fact(DisplayName = "Sets Brighter-native MessageType / MessageId / Topic / HandledCount headers")]
    public void CreateWrappedMessage_SetsBrighterNativeHeaders()
    {
        var adapter = new BrighterAdapter();
        var request = NewRequest();

        adapter.CreateWrappedMessage(request);

        request.Headers.Should().ContainKey("MessageId");
        request.Headers["MessageId"].Should().MatchRegex(@"^[0-9a-fA-F-]{36}$");
        request.Headers["MessageType"].Should().Be("MT_EVENT");
        request.Headers["HandledCount"].Should().Be("0");
        request.Headers["Topic"].Should().Be("OrderPlaced");
    }

    [Fact(DisplayName = "Defaults Topic to MessageType when Properties['topic'] is absent")]
    public void CreateWrappedMessage_FallsBackToMessageTypeForTopic()
    {
        var adapter = new BrighterAdapter();
        var request = NewRequest();

        adapter.CreateWrappedMessage(request);

        request.Headers["Topic"].Should().Be("OrderPlaced");
    }

    [Fact(DisplayName = "Derives Topic from Properties['topic'] when supplied")]
    public void CreateWrappedMessage_DerivesTopicFromPropertiesWhenPresent()
    {
        var adapter = new BrighterAdapter();
        var request = NewRequest(properties: new Dictionary<string, string>
        {
            ["topic"] = "orders.placed",
        });

        adapter.CreateWrappedMessage(request);

        request.Headers["Topic"].Should().Be("orders.placed");
    }

    [Fact(DisplayName = "Respects Properties['BrighterMessageType'] override (e.g. MT_COMMAND)")]
    public void CreateWrappedMessage_RespectsBrighterMessageTypeProperty()
    {
        var adapter = new BrighterAdapter();
        var request = NewRequest(properties: new Dictionary<string, string>
        {
            ["BrighterMessageType"] = "MT_COMMAND",
        });

        adapter.CreateWrappedMessage(request);

        request.Headers["MessageType"].Should().Be("MT_COMMAND");
    }

    [Fact(DisplayName = "Generates a CorrelationId mirroring MessageId when none supplied")]
    public void CreateWrappedMessage_GeneratesCorrelationIdMirroringMessageId()
    {
        var adapter = new BrighterAdapter();
        var request = NewRequest();

        adapter.CreateWrappedMessage(request);

        request.Headers["CorrelationId"].Should().Be(request.Headers["MessageId"]);
    }

    [Fact(DisplayName = "Preserves a caller-supplied CorrelationId")]
    public void CreateWrappedMessage_PreservesCallerCorrelationId()
    {
        var adapter = new BrighterAdapter();
        var request = NewRequest(headers: new Dictionary<string, string>
        {
            ["CorrelationId"] = "caller-correlation-123",
        });

        adapter.CreateWrappedMessage(request);

        request.Headers["CorrelationId"].Should().Be("caller-correlation-123");
        request.Headers["MessageId"].Should().NotBe("caller-correlation-123");
    }

    [Fact(DisplayName = "Falls back to MessageType when MessageFullyQualifiedName is null")]
    public void CreateWrappedMessage_FallsBackToMessageTypeForCloudEventsType()
    {
        var adapter = new BrighterAdapter();
        var request = NewRequest(fullyQualifiedName: null);

        adapter.CreateWrappedMessage(request);

        request.Headers["cloudEvents_type"].Should().Be("OrderPlaced");
    }

    [Fact(DisplayName = "Throws when Json is empty")]
    public void CreateWrappedMessage_ThrowsOnEmptyJson()
    {
        var adapter = new BrighterAdapter();
        var request = NewRequest();
        request.Json = "";

        var act = () => adapter.CreateWrappedMessage(request);

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Throws when MessageType is empty")]
    public void CreateWrappedMessage_ThrowsOnEmptyMessageType()
    {
        var adapter = new BrighterAdapter();
        var request = NewRequest();
        request.MessageType = "";

        var act = () => adapter.CreateWrappedMessage(request);

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Adapter is discovered by AddFrameworkProvider and resolvable by name")]
    public void Registration_DiscoversBrighterAdapter()
    {
        var services = new ServiceCollection();
        services.AddFrameworkProvider();
        using var provider = services.BuildServiceProvider();

        var frameworkProvider = provider.GetRequiredService<IFrameworkProvider>();
        var framework = frameworkProvider.GetFramework("Brighter");

        framework.Should().NotBeNull();
        framework.Should().BeOfType<BrighterAdapter>();
        framework!.Name.Should().Be("Brighter");
    }
}
