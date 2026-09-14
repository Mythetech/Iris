using FluentAssertions;
using Iris.Brokers.Models;
using Iris.Contracts.Messaging.Models;
using Iris.Contracts.Results;
using ReadSource = Iris.Contracts.Messaging.Models.ReadSource;

namespace Iris.Desktop.Test.Brokers;

/// <summary>
/// The four read operations, which all funnel through one private generic helper.
///
/// <para>
/// Making the container tests honest exposed that these four interfaces take a
/// <c>CancellationToken</c> that nothing had ever passed. The token cases below close that:
/// they are the only assertions in the suite that a Cancel in the reader panel reaches the
/// broker SDK rather than being swallowed at Iris's own boundary.
/// </para>
/// </summary>
public class ReadPipelineTests
{
    private const string Endpoint = "orders";

    public enum Operation
    {
        Peek,
        Receive,
        PeekDeadLetter,
        ReceiveDeadLetter,
    }

    private static Task<Result<IReadOnlyList<ReceivedMessageDto>>> Invoke(
        ConnectionManagerHarness harness, Operation operation, string address, CancellationToken cancellationToken, int count = 10)
        => operation switch
        {
            Operation.Peek => harness.Manager.PeekMessagesAsync(address, Endpoint, count, cancellationToken),
            Operation.Receive => harness.Manager.ReceiveMessagesAsync(address, Endpoint, count, cancellationToken),
            Operation.PeekDeadLetter => harness.Manager.PeekDeadLetterMessagesAsync(address, Endpoint, count, cancellationToken),
            Operation.ReceiveDeadLetter => harness.Manager.ReceiveDeadLetterMessagesAsync(address, Endpoint, count, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

    [Theory(DisplayName = "Reading from an address with no connection fails the same way for every operation")]
    [InlineData(Operation.Peek)]
    [InlineData(Operation.Receive)]
    [InlineData(Operation.PeekDeadLetter)]
    [InlineData(Operation.ReceiveDeadLetter)]
    public async Task Unknown_address_fails(Operation operation)
    {
        using var harness = new ConnectionManagerHarness();

        var result = await Invoke(harness, operation, "amqp://nothing-here", TestContext.Current.CancellationToken);

        result.Should().BeOfType<Failure<IReadOnlyList<ReceivedMessageDto>>>()
            .Which.Message.Should().Be("Connection not found.");
    }

    [Theory(DisplayName = "A broker without the capability says so by name rather than returning nothing")]
    [InlineData(Operation.Peek, "AzureQueueStorage does not support peek.")]
    [InlineData(Operation.Receive, "AzureQueueStorage does not support receive.")]
    [InlineData(Operation.PeekDeadLetter, "AzureQueueStorage does not support dead-letter peek.")]
    [InlineData(Operation.ReceiveDeadLetter, "AzureQueueStorage does not support dead-letter receive.")]
    public async Task Missing_capability_is_named(Operation operation, string expected)
    {
        using var harness = new ConnectionManagerHarness();

        // A plain connection implements none of the reader interfaces, which is exactly how
        // a broker opts out: there is no flag to set and no NotSupportedException to throw.
        var connection = harness.AtItsAddress(new RecordingConnection("AzureQueueStorage"));

        var result = await Invoke(harness, operation, connection.Address, TestContext.Current.CancellationToken);

        result.Should().BeOfType<Failure<IReadOnlyList<ReceivedMessageDto>>>()
            .Which.Message.Should().Be(expected);
    }

    [Theory(DisplayName = "A main-queue read is clamped to the batch size the broker will honour")]
    [InlineData(Operation.Peek, 32)]
    [InlineData(Operation.Receive, 16)]
    public async Task Main_queue_reads_are_clamped(Operation operation, int expected)
    {
        using var harness = new ConnectionManagerHarness();
        var connection = harness.AtItsAddress(new ReadingConnection { MaxPeekBatchSize = 32, MaxReceiveBatchSize = 16 });

        await Invoke(harness, operation, connection.Address, TestContext.Current.CancellationToken, count: 500);

        // Service Bus caps a peek and SQS caps a receive at ten. Asking for more is not an
        // error at the SDK, it just quietly returns the cap, so the clamp is what keeps the
        // count the user typed from looking like a broker that lost messages.
        connection.RequestedCount.Should().Be(expected);
    }

    [Theory(DisplayName = "A dead-letter read passes the count through, having no declared cap")]
    [InlineData(Operation.PeekDeadLetter)]
    [InlineData(Operation.ReceiveDeadLetter)]
    public async Task Dead_letter_reads_are_not_clamped(Operation operation)
    {
        using var harness = new ConnectionManagerHarness();
        var connection = harness.AtItsAddress(new ReadingConnection());

        await Invoke(harness, operation, connection.Address, TestContext.Current.CancellationToken, count: 500);

        // The asymmetry is in the interfaces: only IMessagePeeker and IMessageReceiver
        // declare a maximum, so there is nothing here to clamp against.
        connection.RequestedCount.Should().Be(500);
    }

    [Theory(DisplayName = "A read under the cap is passed through unchanged")]
    [InlineData(Operation.Peek)]
    [InlineData(Operation.Receive)]
    public async Task Small_reads_are_untouched(Operation operation)
    {
        using var harness = new ConnectionManagerHarness();
        var connection = harness.AtItsAddress(new ReadingConnection());

        await Invoke(harness, operation, connection.Address, TestContext.Current.CancellationToken, count: 3);

        connection.RequestedCount.Should().Be(3);
    }

    [Theory(DisplayName = "The caller's cancellation token reaches the broker")]
    [InlineData(Operation.Peek)]
    [InlineData(Operation.Receive)]
    [InlineData(Operation.PeekDeadLetter)]
    [InlineData(Operation.ReceiveDeadLetter)]
    public async Task The_token_reaches_the_broker(Operation operation)
    {
        using var harness = new ConnectionManagerHarness();
        var connection = harness.AtItsAddress(new ReadingConnection());
        using var cts = new CancellationTokenSource();

        await Invoke(harness, operation, connection.Address, cts.Token);

        // Passing CancellationToken.None here would compile, satisfy every other assertion
        // in this file, and leave a Cancel in the reader panel doing nothing.
        connection.ObservedToken.Should().Be(cts.Token);
    }

    [Fact(DisplayName = "The endpoint the reader is given carries the name and the provider")]
    public async Task The_endpoint_is_described_to_the_reader()
    {
        using var harness = new ConnectionManagerHarness();
        var connection = harness.AtItsAddress(new ReadingConnection { Connector = new FakeConnector("AzureServiceBus") });

        await harness.Manager.PeekMessagesAsync(connection.Address, "orders", 5, TestContext.Current.CancellationToken);

        connection.ReadFrom!.Name.Should().Be("orders");
        connection.ReadFrom.Address.Should().Be(connection.Address);
        connection.ReadFrom.Provider.Should().Be("AzureServiceBus");
    }

    [Fact(DisplayName = "A broker error during a read is reported, not thrown")]
    public async Task A_broker_error_is_reported()
    {
        using var harness = new ConnectionManagerHarness();
        var connection = harness.AtItsAddress(new ReadingConnection());
        connection.ReadThrows = new InvalidOperationException("the queue was deleted");

        var result = await harness.Manager.PeekMessagesAsync(connection.Address, Endpoint, 10, TestContext.Current.CancellationToken);

        result.Should().BeOfType<Failure<IReadOnlyList<ReceivedMessageDto>>>()
            .Which.Message.Should().Be("peek failed: the queue was deleted");
    }

    [Fact(DisplayName = "Cancelling a read propagates rather than looking like a broker error")]
    public async Task Cancellation_propagates()
    {
        using var harness = new ConnectionManagerHarness();
        var connection = harness.AtItsAddress(new ReadingConnection());
        connection.ReadThrows = new OperationCanceledException();

        var read = async () => await harness.Manager.ReceiveMessagesAsync(connection.Address, Endpoint, 10, TestContext.Current.CancellationToken);

        await read.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact(DisplayName = "Every field of a read message survives the trip to the contract layer")]
    public async Task Messages_are_mapped_whole()
    {
        var enqueued = DateTimeOffset.UtcNow.AddMinutes(-5);
        var expires = DateTimeOffset.UtcNow.AddHours(1);

        using var harness = new ConnectionManagerHarness();
        var connection = harness.AtItsAddress(new ReadingConnection
        {
            Result =
            [
                new ReceivedMessage
                {
                    Body = """{"id":1}""",
                    MessageId = "m-1",
                    CorrelationId = "c-1",
                    ContentType = "application/json",
                    Headers = new Dictionary<string, string> { ["tenant"] = "contoso" },
                    Properties = new Dictionary<string, string> { ["origin"] = "iris" },
                    DeliveryCount = 3,
                    EnqueuedTimeUtc = enqueued,
                    ExpiresAtUtc = expires,
                    SizeInBytes = 42,
                    Source = Iris.Brokers.Models.ReadSource.DeadLetter,
                    Provider = "AzureServiceBus",
                },
            ],
        });

        var result = await harness.Manager.PeekDeadLetterMessagesAsync(connection.Address, Endpoint, 10, TestContext.Current.CancellationToken);

        var message = result.Should().BeOfType<Success<IReadOnlyList<ReceivedMessageDto>>>()
            .Which.Value.Should().ContainSingle().Subject;

        message.Body.Should().Be("""{"id":1}""");
        message.MessageId.Should().Be("m-1");
        message.CorrelationId.Should().Be("c-1");
        message.ContentType.Should().Be("application/json");
        message.Headers.Should().Contain(new KeyValuePair<string, string>("tenant", "contoso"));
        message.Properties.Should().Contain(new KeyValuePair<string, string>("origin", "iris"));
        message.DeliveryCount.Should().Be(3);
        message.EnqueuedTimeUtc.Should().Be(enqueued);
        message.ExpiresAtUtc.Should().Be(expires);
        message.SizeInBytes.Should().Be(42);
        message.Provider.Should().Be("AzureServiceBus");
        message.Source.Should().Be(ReadSource.DeadLetter);
    }

    [Fact(DisplayName = "The two ReadSource enums cannot drift apart unnoticed")]
    public void The_read_source_enums_line_up()
    {
        // The mapper casts one enum to the other by ordinal. Reordering either declaration
        // would relabel dead-lettered messages as live ones, with no compiler error and no
        // visible symptom beyond a wrong badge in the reader.
        Enum.GetValues<Iris.Brokers.Models.ReadSource>()
            .Select(source => (source.ToString(), (int)source))
            .Should().Equal(Enum.GetValues<ReadSource>().Select(source => (source.ToString(), (int)source)));
    }

    [Fact(DisplayName = "Native broker metadata is flattened onto the DTO for display")]
    public async Task Native_metadata_is_flattened()
    {
        using var harness = new ConnectionManagerHarness();
        var connection = harness.AtItsAddress(new ReadingConnection
        {
            Result =
            [
                new ReceivedMessage
                {
                    Body = "{}",
                    Native = new NativeMessageMetadata
                    {
                        LockToken = "lock",
                        ReceiptHandle = "receipt",
                        PopReceipt = "pop",
                        RoutingKey = "orders.created",
                        Exchange = "orders",
                        Extra = new Dictionary<string, string> { ["Redelivered"] = "true" },
                    },
                },
            ],
        });

        var result = await harness.Manager.PeekMessagesAsync(connection.Address, Endpoint, 1, TestContext.Current.CancellationToken);

        var message = result.Should().BeOfType<Success<IReadOnlyList<ReceivedMessageDto>>>()
            .Which.Value.Should().ContainSingle().Subject;

        message.Native.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["LockToken"] = "lock",
            ["ReceiptHandle"] = "receipt",
            ["PopReceipt"] = "pop",
            ["RoutingKey"] = "orders.created",
            ["Exchange"] = "orders",
            ["Redelivered"] = "true",
        });
    }

    [Fact(DisplayName = "A message with no native metadata maps to an empty bag, not a null one")]
    public async Task Absent_native_metadata_is_empty()
    {
        using var harness = new ConnectionManagerHarness();
        var connection = harness.AtItsAddress(new ReadingConnection
        {
            Result = [new ReceivedMessage { Body = "{}" }],
        });

        var result = await harness.Manager.PeekMessagesAsync(connection.Address, Endpoint, 1, TestContext.Current.CancellationToken);

        result.Should().BeOfType<Success<IReadOnlyList<ReceivedMessageDto>>>()
            .Which.Value.Should().ContainSingle()
            .Which.Native.Should().BeEmpty();
    }

    [Fact(DisplayName = "Capabilities report the interfaces the connection implements")]
    public async Task Capabilities_describe_a_full_reader()
    {
        using var harness = new ConnectionManagerHarness();
        var connection = harness.AtItsAddress(new ReadingConnection { MaxPeekBatchSize = 32, MaxReceiveBatchSize = 16 });

        var capabilities = await harness.Manager.GetReaderCapabilitiesAsync(connection.Address);

        capabilities.Should().NotBeNull();
        capabilities!.CanPeek.Should().BeTrue();
        capabilities.CanReceive.Should().BeTrue();
        capabilities.CanPeekDeadLetter.Should().BeTrue();
        capabilities.CanReceiveDeadLetter.Should().BeTrue();
        capabilities.MaxPeekBatchSize.Should().Be(32);
        capabilities.MaxReceiveBatchSize.Should().Be(16);
    }

    [Fact(DisplayName = "A connection that reads nothing reports every capability off and no batch sizes")]
    public async Task Capabilities_describe_a_write_only_broker()
    {
        using var harness = new ConnectionManagerHarness();
        var connection = harness.AtItsAddress(new RecordingConnection());

        var capabilities = await harness.Manager.GetReaderCapabilitiesAsync(connection.Address);

        capabilities.Should().NotBeNull();
        capabilities!.CanPeek.Should().BeFalse();
        capabilities.CanReceive.Should().BeFalse();
        capabilities.CanPeekDeadLetter.Should().BeFalse();
        capabilities.CanReceiveDeadLetter.Should().BeFalse();

        // Zero rather than a default cap, so the reader panel cannot offer a batch size for
        // an operation the broker will refuse.
        capabilities.MaxPeekBatchSize.Should().Be(0);
        capabilities.MaxReceiveBatchSize.Should().Be(0);
    }

    [Fact(DisplayName = "Capabilities for an address with no connection are absent, not empty")]
    public async Task Capabilities_of_an_unknown_address_are_null()
    {
        using var harness = new ConnectionManagerHarness();

        var capabilities = await harness.Manager.GetReaderCapabilitiesAsync("amqp://nothing-here");

        // Null and an all-false record mean different things to the caller: one is "not
        // connected", the other is "connected to a broker that cannot read".
        capabilities.Should().BeNull();
    }
}
