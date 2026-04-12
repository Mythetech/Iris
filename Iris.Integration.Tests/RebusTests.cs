using System;
using System.Threading.Tasks;
using FluentAssertions;
using Iris.Brokers;
using Iris.Brokers.Frameworks;
using Iris.Brokers.Models;
using Iris.Brokers.RabbitMQ;
using Iris.Integration.Tests.Fixtures;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Routing.TypeBased;
using IRebusHandler = Rebus.Handlers.IHandleMessages<Iris.Integration.Tests.IrisRebusTestMessage>;
using Testcontainers.RabbitMq;

namespace Iris.Integration.Tests
{
    // Top-level so its assembly-qualified name (FullName + Assembly.GetName().Name)
    // matches what RebusAdapter emits as the rbs2-msg-type header. The receiving Rebus
    // bus uses SimpleAssemblyQualifiedMessageTypeNameConvention by default and resolves
    // the type via Type.GetType(name), so the type must be loadable from the test assembly.
    public record IrisRebusTestMessage(int Red, int Green, int Blue);

    [Collection("RabbitMQ")]
    [Trait("Category", "Container")]
    public class RebusTests : IAsyncLifetime
    {
        private const string QueueName = "iris-rebus-test";

        private readonly RabbitMqContainer _rabbitMqContainer;

        private BuiltinHandlerActivator? _activator;
        private IDisposable? _bus;
        private readonly TaskCompletionSource<IrisRebusTestMessage> _received =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public RebusTests(RabbitMqContainerFixture fixture)
        {
            _rabbitMqContainer = fixture.Container;
        }

        public async Task InitializeAsync()
        {
            var amqpPort = _rabbitMqContainer.GetMappedPublicPort(5672);
            var amqpUri = $"amqp://guest:guest@localhost:{amqpPort}";

            _activator = new BuiltinHandlerActivator();
            _activator.Register(() => new IrisRebusTestHandler(_received));

            _bus = Configure.With(_activator)
                .Transport(t => t.UseRabbitMq(amqpUri, QueueName))
                .Routing(r => r.TypeBased().Map<IrisRebusTestMessage>(QueueName))
                .Start();
        }

        public Task DisposeAsync()
        {
            _bus?.Dispose();
            _activator?.Dispose();
            return Task.CompletedTask;
        }

        [Fact(DisplayName = "Iris Rebus-wrapped message round-trips to a real Rebus consumer on RabbitMQ")]
        public async Task Can_Consume_Rebus_Message()
        {
            // Arrange — wrap a message exactly as LocalConnectionManager.SendMessageAsync does.
            var adapter = new RebusAdapter();

            var messageType = typeof(IrisRebusTestMessage);

            var request = MessageRequest.Create(
                messageType: messageType.Name,
                json: "{\"Red\":1,\"Green\":2,\"Blue\":3}",
                generateIrisHeaders: false,
                messageFullyQualifiedName: messageType.FullName,
                framework: "Rebus",
                messageAssemblyName: messageType.Assembly.GetName().Name);

            request.WrapMessage(adapter);

            var managementPort = _rabbitMqContainer.GetMappedPublicPort(15672);
            var connectionData = new ConnectionData
            {
                ConnectionString = $"http://localhost:{managementPort}",
                Username = "guest",
                Password = "guest",
            };

            var connector = new RabbitMqConnector();
            var connection = await connector.ConnectAsync(connectionData, false);
            connection.Should().NotBeNull("RabbitMqConnector must connect to the management API");

            // Act — publish the wrapped envelope to the queue Rebus is consuming from.
            await connection!.SendAsync(
                new EndpointDetails
                {
                    Provider = "rabbitmq",
                    Address = $"http://localhost:{managementPort}",
                    Name = QueueName,
                    Type = "Queue",
                },
                request);

            // Assert — the Rebus consumer must deserialize and receive the payload.
            var completed = await Task.WhenAny(
                _received.Task,
                Task.Delay(TimeSpan.FromSeconds(30)));

            completed.Should().BeSameAs(
                _received.Task,
                "Rebus should consume the Iris-wrapped envelope within 30s — a timeout means the adapter produced a wire format Rebus can't route or deserialize");

            var message = await _received.Task;
            message.Red.Should().Be(1);
            message.Green.Should().Be(2);
            message.Blue.Should().Be(3);
        }

        private sealed class IrisRebusTestHandler : IRebusHandler
        {
            private readonly TaskCompletionSource<IrisRebusTestMessage> _received;

            public IrisRebusTestHandler(TaskCompletionSource<IrisRebusTestMessage> received)
            {
                _received = received;
            }

            public Task Handle(IrisRebusTestMessage message)
            {
                _received.TrySetResult(message);
                return Task.CompletedTask;
            }
        }
    }
}
