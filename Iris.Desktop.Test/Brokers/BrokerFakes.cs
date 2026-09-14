using Iris.Brokers;
using Iris.Brokers.Frameworks;
using Iris.Brokers.Models;
using EndpointDetails = Iris.Brokers.EndpointDetails;

namespace Iris.Desktop.Test.Brokers;

/// <summary>
/// Hand-written stand-ins rather than substitutes, because the thing under test is which
/// interfaces a connection implements. <see cref="LocalConnectionManager"/> decides what a
/// broker can do by pattern matching (<c>connection is IMessagePeeker</c>), so a fake has
/// to be able to genuinely not implement a capability, and the send path reads the request
/// object after the framework has rewritten it, which a received-call assertion cannot show.
/// </summary>
public class FakeConnector : IConnector
{
    public FakeConnector(string provider = "FakeBroker") => Provider = provider;

    public string Provider { get; }

    /// <summary>What <see cref="ConnectAsync"/> hands back. Null models a connector that
    /// declines without throwing, which the create path treats as its own failure.</summary>
    public IConnection? Connection { get; set; }

    public Exception? ConnectThrows { get; set; }

    public ConnectionData? LastConnectionData { get; private set; }

    public Task<IConnection?> ConnectAsync(ConnectionData data, bool discoverEndpoints = true)
    {
        LastConnectionData = data;

        if (ConnectThrows is not null)
            throw ConnectThrows;

        return Task.FromResult(Connection);
    }

    public Task<IConnection?> ConnectAsync(ConnectionData data, CancellationToken cancellationToken, bool discoverEndpoints = true)
        => ConnectAsync(data, discoverEndpoints);
}

/// <summary>
/// A connection that carries nothing beyond the body and records what it was asked to send.
/// The carrier-free shape is deliberate: it is what makes an adapter's optional keys get
/// dropped and its required ones fail, so it is the baseline the compatibility tests vary from.
/// </summary>
public class RecordingConnection : IConnection
{
    public RecordingConnection(string provider = "FakeBroker")
    {
        Connector = new FakeConnector(provider);
    }

    public IConnector Connector { get; set; }

    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; init; } = "fake";

    public string Address { get; init; } = "fake://broker";

    public int EndpointCount { get; init; }

    public List<EndpointDetails> Endpoints { get; init; } = [];

    public Exception? SendThrows { get; set; }

    public EndpointDetails? SentTo { get; private set; }

    /// <summary>
    /// The request as the broker saw it. Held by reference on purpose: the send path mutates
    /// the same instance the framework wrapped, so this is the only way to see the final
    /// headers and properties rather than the ones the caller passed in.
    /// </summary>
    public MessageRequest? Sent { get; private set; }

    public Task<List<EndpointDetails>> GetEndpointsAsync() => Task.FromResult(Endpoints);

    public Task SendAsync(EndpointDetails endpoint, MessageRequest message)
    {
        SentTo = endpoint;
        Sent = message;

        if (SendThrows is not null)
            throw SendThrows;

        return Task.CompletedTask;
    }
}

/// <summary>A connection that maps headers, with the limits under the test's control.</summary>
public sealed class HeaderCarryingConnection : RecordingConnection, IHeaderCarrier
{
    public int MaxHeaderCount { get; set; } = 10;

    public Func<string, bool> KeyValidator { get; set; } = _ => true;

    public IReadOnlySet<HeaderDataType> SupportedDataTypes { get; set; } =
        new HashSet<HeaderDataType>(Enum.GetValues<HeaderDataType>());

    public bool IsValidHeaderKey(string key) => KeyValidator(key);
}

/// <summary>
/// A connection that can read. Every capability is opt-in so a test can build the exact
/// combination it needs, including the combinations no real broker has, which is what
/// pins the capability probe rather than any one provider's behaviour.
/// </summary>
public sealed class ReadingConnection : RecordingConnection, IMessagePeeker, IMessageReceiver, IDeadLetterPeeker, IDeadLetterReceiver
{
    public int MaxPeekBatchSize { get; set; } = 32;

    public int MaxReceiveBatchSize { get; set; } = 16;

    public Exception? ReadThrows { get; set; }

    public IReadOnlyList<ReceivedMessage> Result { get; set; } = [];

    public int RequestedCount { get; private set; }

    public EndpointDetails? ReadFrom { get; private set; }

    public CancellationToken ObservedToken { get; private set; }

    public Task<IReadOnlyList<ReceivedMessage>> PeekAsync(EndpointDetails endpoint, int count, CancellationToken cancellationToken = default)
        => Read(endpoint, count, cancellationToken);

    public Task<IReadOnlyList<ReceivedMessage>> ReceiveAsync(EndpointDetails endpoint, int count, CancellationToken cancellationToken = default)
        => Read(endpoint, count, cancellationToken);

    public Task<IReadOnlyList<ReceivedMessage>> PeekDeadLetterAsync(EndpointDetails endpoint, int count, CancellationToken cancellationToken = default)
        => Read(endpoint, count, cancellationToken);

    public Task<IReadOnlyList<ReceivedMessage>> ReceiveDeadLetterAsync(EndpointDetails endpoint, int count, CancellationToken cancellationToken = default)
        => Read(endpoint, count, cancellationToken);

    private Task<IReadOnlyList<ReceivedMessage>> Read(EndpointDetails endpoint, int count, CancellationToken cancellationToken)
    {
        ReadFrom = endpoint;
        RequestedCount = count;
        ObservedToken = cancellationToken;

        if (ReadThrows is not null)
            throw ReadThrows;

        return Task.FromResult(Result);
    }
}

/// <summary>
/// A framework whose keys and wrapping are supplied by the test. The real adapters are
/// covered in <c>Iris.Brokers.Test</c>; what matters here is how the send path reacts to
/// what an adapter declares, so declaring it directly keeps the cause visible in the test.
/// </summary>
public sealed class FakeFramework : IFramework, IFrameworkProvider
{
    public string Name { get; init; } = "FakeFramework";

    public IReadOnlyList<FrameworkKey> Keys { get; init; } = [];

    public Iris.Contracts.Messaging.Frameworks.FrameworkDescriptor Descriptor =>
        new(Name, []);

    public IReadOnlySet<string> VerifiedProviders { get; init; } = new HashSet<string>();

    public Exception? WrapThrows { get; set; }

    public string WrappedBody { get; set; } = """{"wrapped":true}""";

    public string CreateWrappedMessage(IMessageRequest request)
    {
        if (WrapThrows is not null)
            throw WrapThrows;

        foreach (var key in Keys)
        {
            if (key.Location == KeyLocation.Header)
                request.Headers[key.Name] = "written-by-the-framework";
            else if (key.Property == TransportProperty.MessageId)
                request.TransportProperties.MessageId = "written-by-the-framework";
        }

        return WrappedBody;
    }

    /// <summary>Answers for its own name only, so an unknown name returns null the way the real provider does.</summary>
    public IFramework? GetFramework(string framework)
        => string.Equals(framework, Name, StringComparison.OrdinalIgnoreCase) ? this : null;
}
