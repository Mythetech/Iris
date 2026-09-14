using FluentAssertions;
using Iris.Brokers.Frameworks;
using Iris.Brokers.Models;
using Iris.Contracts.Messaging.Events;
using Iris.Contracts.Messaging.Frameworks;
using Iris.Contracts.Messaging.Models;
using Iris.Contracts.Results;

namespace Iris.Desktop.Test.Brokers;

/// <summary>
/// The send path in <c>LocalConnectionManager</c>: everything between the user pressing
/// Send and the broker being handed a body.
///
/// <para>
/// It is the only place where the user's message type, the framework envelope, the
/// transport's limits and the history record all meet, and it had no tests. The cases
/// below are chosen for what fails silently: a wrapped body that gets recorded unwrapped,
/// a failed send that gets recorded as a success, a framework key that a transport quietly
/// drops. None of those throw, and none of them are visible in the UI.
/// </para>
/// </summary>
public class SendPipelineTests
{
    private static Message AMessage(string address, string type = "OrderPlaced") => new()
    {
        MessageType = type,
        Address = address,
        Data = """{"id":1}""",
    };

    [Fact(DisplayName = "An address with no connection behind it fails before anything is sent")]
    public async Task Unknown_address_fails()
    {
        using var harness = new ConnectionManagerHarness();

        var result = await harness.Manager.SendMessageAsync(AMessage("amqp://nothing-here"));

        result.Should().BeOfType<Failure<bool>>()
            .Which.Message.Should().Be("Connection not found.");
        harness.Published<MessageSent>().Should().BeEmpty();
    }

    [Fact(DisplayName = "A framework the provider does not know fails without sending")]
    public async Task Unknown_framework_fails()
    {
        var framework = new FakeFramework();
        using var harness = new ConnectionManagerHarness(framework);
        var connection = harness.AtItsAddress(new RecordingConnection());

        var message = AMessage(connection.Address);
        message.Framework = "SomethingElse";

        var result = await harness.Manager.SendMessageAsync(message);

        result.Should().BeOfType<Failure<bool>>()
            .Which.Message.Should().Be("Unknown framework 'SomethingElse'.");
        connection.Sent.Should().BeNull("an unresolvable envelope must not be sent unwrapped");
    }

    [Fact(DisplayName = "A framework the transport cannot carry fails with the reason, and sends nothing")]
    public async Task Incompatible_framework_fails()
    {
        // MassTransit-shaped: the envelope's routing metadata rides in transport headers, so
        // a body-only transport cannot deliver a message the consumer can route.
        var framework = new FakeFramework
        {
            Name = "NeedsHeaders",
            Keys = [FrameworkKey.Header("message-type", required: true)],
        };
        using var harness = new ConnectionManagerHarness(framework);
        var connection = harness.AtItsAddress(new RecordingConnection("BodyOnlyBroker"));

        var message = AMessage(connection.Address);
        message.Framework = framework.Name;

        var result = await harness.Manager.SendMessageAsync(message);

        result.Should().BeOfType<Failure<bool>>()
            .Which.Message.Should().Be("NeedsHeaders needs transport headers; BodyOnlyBroker cannot carry them.");
        connection.Sent.Should().BeNull();
        harness.Published<MessageSent>().Should().BeEmpty();
    }

    [Fact(DisplayName = "Optional keys the transport cannot carry are stripped from the request before it is sent")]
    public async Task Dropped_keys_do_not_reach_the_broker()
    {
        var framework = new FakeFramework
        {
            Name = "MostlyOptional",
            Keys =
            [
                FrameworkKey.Header("nice-to-have"),
                FrameworkKey.Transport(TransportProperty.MessageId),
            ],
        };
        using var harness = new ConnectionManagerHarness(framework);
        var connection = harness.AtItsAddress(new RecordingConnection());

        var message = AMessage(connection.Address);
        message.Framework = framework.Name;

        var result = await harness.Manager.SendMessageAsync(message);

        result.Should().BeOfType<Success<bool>>();

        // The framework writes both on wrap; the connection carries neither, so the send
        // must not present values the transport will silently discard.
        connection.Sent!.Headers.Should().NotContainKey("nice-to-have");
        connection.Sent.TransportProperties.MessageId.Should().BeNull();
    }

    [Fact(DisplayName = "A framework that rejects the message is reported, not thrown")]
    public async Task Wrap_failure_becomes_a_failure()
    {
        var framework = new FakeFramework
        {
            Name = "Strict",
            WrapThrows = new ArgumentException("NServiceBus needs a fully qualified type name."),
        };
        using var harness = new ConnectionManagerHarness(framework);
        var connection = harness.AtItsAddress(new RecordingConnection());

        var message = AMessage(connection.Address);
        message.Framework = framework.Name;

        var result = await harness.Manager.SendMessageAsync(message);

        result.Should().BeOfType<Failure<bool>>()
            .Which.Message.Should().Be("NServiceBus needs a fully qualified type name.");
        connection.Sent.Should().BeNull();
    }

    [Theory(DisplayName = "The endpoint type comes from the properties bag and is not sent on as one")]
    [InlineData("Topic")]
    [InlineData("Exchange")]
    public async Task Endpoint_type_is_consumed(string endpointType)
    {
        using var harness = new ConnectionManagerHarness();
        var connection = harness.AtItsAddress(new RecordingConnection());

        var message = AMessage(connection.Address);
        message.Properties = new Dictionary<string, string> { ["EndpointType"] = endpointType };

        await harness.Manager.SendMessageAsync(message);

        connection.SentTo!.Type.Should().Be(endpointType);
        connection.Sent!.Properties.Should().NotContainKey("EndpointType",
            "it is routing instruction for Iris, not payload metadata for the broker");
    }

    [Fact(DisplayName = "Without an endpoint type the send defaults to a queue")]
    public async Task Endpoint_type_defaults_to_queue()
    {
        using var harness = new ConnectionManagerHarness();
        var connection = harness.AtItsAddress(new RecordingConnection("RabbitMQ"));

        await harness.Manager.SendMessageAsync(AMessage(connection.Address, "OrderPlaced"));

        connection.SentTo!.Type.Should().Be("Queue");
        connection.SentTo.Name.Should().Be("OrderPlaced");
        connection.SentTo.Provider.Should().Be("RabbitMQ");
        connection.SentTo.Address.Should().Be(connection.Address);
    }

    [Fact(DisplayName = "An explicit type name overrides the message type without replacing it")]
    public async Task Explicit_type_name_wins()
    {
        using var harness = new ConnectionManagerHarness();
        var connection = harness.AtItsAddress(new RecordingConnection());

        var message = AMessage(connection.Address, "OrderPlaced");
        message.Properties = new Dictionary<string, string>
        {
            [FrameworkInputs.TypeName] = "Contoso.Orders.OrderPlaced",
            [FrameworkInputs.AssemblyName] = "Contoso.Orders",
        };

        await harness.Manager.SendMessageAsync(message);

        // Two different names on purpose: the endpoint keeps the short one the user typed,
        // while the envelope needs the assembly-qualified one the consumer will resolve.
        connection.Sent!.MessageType.Should().Be("OrderPlaced");
        connection.Sent.MessageFullyQualifiedName.Should().Be("Contoso.Orders.OrderPlaced");
        connection.Sent.MessageAssemblyName.Should().Be("Contoso.Orders");
    }

    [Theory(DisplayName = "A blank override falls back rather than sending an empty name")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_overrides_fall_back(string blank)
    {
        using var harness = new ConnectionManagerHarness();
        var connection = harness.AtItsAddress(new RecordingConnection());

        var message = AMessage(connection.Address, "OrderPlaced");
        message.Properties = new Dictionary<string, string> { [FrameworkInputs.TypeName] = blank };

        await harness.Manager.SendMessageAsync(message);

        connection.Sent!.MessageFullyQualifiedName.Should().Be("OrderPlaced");
    }

    [Fact(DisplayName = "A send that the broker rejects is not recorded as sent")]
    public async Task A_failed_send_is_not_recorded()
    {
        using var harness = new ConnectionManagerHarness();
        var connection = harness.AtItsAddress(new RecordingConnection());
        connection.SendThrows = new InvalidOperationException("queue does not exist");

        var result = await harness.Manager.SendMessageAsync(AMessage(connection.Address));

        result.Should().BeOfType<Failure<bool>>()
            .Which.Message.Should().Be("send failed: queue does not exist");

        // MessageSent is what the history pane and the telemetry sink both consume. A
        // failed send that still published it would leave the user with a record of a
        // message no broker ever accepted.
        harness.Published<MessageSent>().Should().BeEmpty();
    }

    [Fact(DisplayName = "Cancellation during a send propagates instead of being reported as a broker error")]
    public async Task Cancellation_propagates()
    {
        using var harness = new ConnectionManagerHarness();
        var connection = harness.AtItsAddress(new RecordingConnection());
        connection.SendThrows = new OperationCanceledException();

        var send = async () => await harness.Manager.SendMessageAsync(AMessage(connection.Address));

        // A cancelled send is the caller giving up, not the broker refusing. Flattening it
        // into a Failure would put "send failed" in front of a user who pressed Cancel.
        await send.Should().ThrowAsync<OperationCanceledException>();
        harness.Published<MessageSent>().Should().BeEmpty();
    }

    [Fact(DisplayName = "A successful send records the body the broker actually got")]
    public async Task History_records_the_wrapped_body()
    {
        var framework = new FakeFramework
        {
            Name = "Wrapping",
            WrappedBody = """{"envelope":{"message":{"id":1}}}""",
        };
        using var harness = new ConnectionManagerHarness(framework);
        var connection = harness.AtItsAddress(new RecordingConnection("RabbitMQ"));

        var message = AMessage(connection.Address, "OrderPlaced");
        message.Framework = framework.Name;

        var result = await harness.Manager.SendMessageAsync(message);

        result.Should().BeOfType<Success<bool>>();

        var sent = harness.Published<MessageSent>().Should().ContainSingle().Subject;

        // Recording message.Data here instead of request.Json would give a history entry
        // that replays as a different message from the one the broker received.
        sent.Message.Should().Be(framework.WrappedBody);
        sent.Address.Should().Be(connection.Address);
        sent.Provider.Should().Be("RabbitMQ");
        sent.Endpoint.Should().Be("OrderPlaced");
    }

    [Theory(DisplayName = "The Iris header follows the messaging setting at the moment of the send")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Iris_header_follows_the_setting(bool enabled)
    {
        using var harness = new ConnectionManagerHarness(sendIrisHeader: enabled);
        var connection = harness.AtItsAddress(new RecordingConnection());

        await harness.Manager.SendMessageAsync(AMessage(connection.Address));

        connection.Sent!.Headers.ContainsKey("iris-key").Should().Be(enabled);
    }

    [Fact(DisplayName = "Toggling the setting after the state was built still changes the next send")]
    public async Task The_setting_is_read_per_send()
    {
        using var harness = new ConnectionManagerHarness(sendIrisHeader: false);
        var connection = harness.AtItsAddress(new RecordingConnection());

        harness.Settings.SendIrisHeader = true;
        await harness.Manager.SendMessageAsync(AMessage(connection.Address));

        connection.Sent!.Headers.Should().ContainKey("iris-key",
            "the send path reads the settings singleton rather than a value captured at construction");
    }

    [Fact(DisplayName = "The user's own headers survive the send")]
    public async Task User_headers_are_sent()
    {
        using var harness = new ConnectionManagerHarness();
        var connection = harness.AtItsAddress(new HeaderCarryingConnection());

        var message = AMessage(connection.Address);
        message.Headers = new Dictionary<string, string> { ["tenant"] = "contoso" };

        await harness.Manager.SendMessageAsync(message);

        connection.Sent!.Headers.Should().Contain(new KeyValuePair<string, string>("tenant", "contoso"));
    }

    [Fact(DisplayName = "A header name the transport rejects fails locally rather than at the broker")]
    public async Task Invalid_user_header_fails_locally()
    {
        var framework = new FakeFramework { Name = "AnyFramework" };
        using var harness = new ConnectionManagerHarness(framework);
        var connection = harness.AtItsAddress(new HeaderCarryingConnection { KeyValidator = key => !key.Contains('-') });
        connection.Connector = new FakeConnector("AmazonSQS");

        var message = AMessage(connection.Address);
        message.Framework = framework.Name;
        message.Headers = new Dictionary<string, string> { ["tenant-id"] = "contoso" };

        var result = await harness.Manager.SendMessageAsync(message);

        result.Should().BeOfType<Failure<bool>>()
            .Which.Message.Should().Be("AmazonSQS rejects header name 'tenant-id'.");
        connection.Sent.Should().BeNull();
    }

    [Fact(DisplayName = "The convenience overload builds the same message the object overload takes")]
    public async Task The_overload_maps_its_arguments()
    {
        using var harness = new ConnectionManagerHarness(sendIrisHeader: true);
        var connection = harness.AtItsAddress(new HeaderCarryingConnection());

        var result = await harness.Manager.SendMessageAsync(
            "OrderPlaced",
            """{"id":7}""",
            connection.Address,
            framework: null,
            properties: new Dictionary<string, string> { ["EndpointType"] = "Topic" },
            headers: new Dictionary<string, string> { ["tenant"] = "contoso" });

        result.Should().BeOfType<Success<bool>>();
        connection.Sent!.MessageType.Should().Be("OrderPlaced");
        connection.Sent.Json.Should().Be("""{"id":7}""");
        connection.Sent.Headers.Should().ContainKey("tenant").And.ContainKey("iris-key");
        connection.SentTo!.Type.Should().Be("Topic");
    }

    [Fact(DisplayName = "The caller's properties dictionary is not mutated by the send")]
    public async Task The_callers_dictionary_is_left_alone()
    {
        using var harness = new ConnectionManagerHarness();
        var connection = harness.AtItsAddress(new RecordingConnection());

        // The messaging page holds this dictionary across sends, so removing EndpointType
        // from the caller's copy would make the second send to a topic go to a queue.
        var properties = new Dictionary<string, string> { ["EndpointType"] = "Topic" };
        var message = AMessage(connection.Address);
        message.Properties = properties;

        await harness.Manager.SendMessageAsync(message);

        properties.Should().ContainKey("EndpointType");
    }
}
