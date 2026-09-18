using Iris.Brokers;
using Iris.Brokers.Frameworks;
using Iris.Components.Messaging;
using Iris.Components.PackageManagement;
using Iris.Contracts.Assemblies;
using Iris.Desktop.Brokers;
using Microsoft.Extensions.Logging.Abstractions;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using EndpointDetails = Iris.Brokers.EndpointDetails;

namespace Iris.Desktop.Test.Brokers;

/// <summary>
/// Builds a <see cref="LocalConnectionManager"/> over fakes, with a real
/// <see cref="ConnectionRepository"/> on a temporary LiteDB file.
///
/// <para>
/// The repository is real rather than substituted because the create and delete paths are
/// half persistence: the interesting question is not whether <c>Save</c> was called but
/// whether a saved connection survives a delete that failed, and only the storage layer
/// can answer that.
/// </para>
/// </summary>
public sealed class ConnectionManagerHarness : IDisposable
{
    private readonly TempDatabase _database = new();

    public ConnectionManagerHarness(IFrameworkProvider? frameworks = null, bool sendIrisHeader = false)
    {
        // NSubstitute auto-substitutes any member that returns an interface, so an
        // unconfigured GetConnectionAsync hands back a phantom IConnection rather than the
        // null the real manager returns for an address nobody is connected to. Left alone,
        // every "not connected" case in this suite would silently test the connected path.
        Connections.GetConnectionAsync(Arg.Any<string>()).Returns(Task.FromResult<IConnection?>(null));
        Connections.GetConnectionsAsync().Returns(Task.FromResult<List<IConnection>>([]));
        Connections.GetEndpointsAsync().Returns(Task.FromResult<List<EndpointDetails>>([]));
        Connections.GetProviders().Returns([]);

        Settings = new MessagingSettings { SendIrisHeader = sendIrisHeader };
        State = new MessageState(Settings, Substitute.For<IMessageBus>());
        Repository = new ConnectionRepository(_database.Context, NullLogger<ConnectionRepository>.Instance);

        Manager = new LocalConnectionManager(
            Connections,
            Bus,
            frameworks ?? Substitute.For<IFrameworkProvider>(),
            Substitute.For<ISampleJsonGenerator>(),
            Substitute.For<IPackageService>(),
            new AssemblySettings(),
            State,
            Repository,
            NullLogger<LocalConnectionManager>.Instance);
    }

    public IBrokerConnectionManager Connections { get; } = Substitute.For<IBrokerConnectionManager>();

    public IMessageBus Bus { get; } = Substitute.For<IMessageBus>();

    public MessagingSettings Settings { get; }

    public MessageState State { get; }

    public ConnectionRepository Repository { get; }

    public LocalConnectionManager Manager { get; }

    /// <summary>
    /// Registers <paramref name="connection"/> as the one living at its own address, and at
    /// nothing else, so a test that sends to the wrong address sees a genuine miss.
    /// </summary>
    public T AtItsAddress<T>(T connection) where T : RecordingConnection
    {
        Connections.GetConnectionAsync(connection.Address).Returns(Task.FromResult<IConnection?>(connection));
        return connection;
    }

    /// <summary>
    /// Everything the manager put on the bus, of one type. Read through
    /// <c>ReceivedCalls</c> rather than a stubbed return so that a publish the code should
    /// not have made shows up as an extra item instead of passing unnoticed.
    /// </summary>
    public IReadOnlyList<T> Published<T>() =>
        Bus.ReceivedCalls()
            .SelectMany(call => call.GetArguments())
            .OfType<T>()
            .ToList();

    public void Dispose()
    {
        State.Dispose();
        _database.Dispose();
    }
}
