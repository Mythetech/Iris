using System;
using System.Threading.Tasks;
using EasyNetQ;
using FluentAssertions;
using Iris.Brokers;
using Iris.Brokers.Frameworks;
using Iris.Brokers.Models;
using Iris.Brokers.RabbitMQ;
using Iris.Integration.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.RabbitMq;

namespace Iris.Integration.Tests
{
    // Top-level so EasyNetQ's DefaultTypeNameSerializer resolves
    // "Iris.Integration.Tests.IrisEasyNetQTestMessage, Iris.Integration.Tests",
    // which is what EasyNetQAdapter writes to the AMQP `type` property.
    public record IrisEasyNetQTestMessage(int Red, int Green, int Blue);

    [Collection("RabbitMQ")]
    [Trait("Category", "Container")]
    public class EasyNetQTests : IAsyncLifetime
    {
        private const string QueueName = "iris-easynetq-test";

        private readonly RabbitMqContainer _rabbitMqContainer;
        private readonly TaskCompletionSource<IrisEasyNetQTestMessage> _received =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private ServiceProvider? _services;
        private IAsyncDisposable? _subscription;

        public EasyNetQTests(RabbitMqContainerFixture fixture)
        {
            _rabbitMqContainer = fixture.Container;
        }

        public async Task InitializeAsync()
        {
            var amqpPort = _rabbitMqContainer.GetMappedPublicPort(5672);

            var services = new ServiceCollection();
            services.AddEasyNetQ($"host=localhost;port={amqpPort};username=guest;password=guest");
            _services = services.BuildServiceProvider();

            var bus = _services.GetRequiredService<IBus>();
            _subscription = await bus.SendReceive.ReceiveAsync<IrisEasyNetQTestMessage>(
                QueueName,
                (message, _) =>
                {
                    _received.TrySetResult(message);
                    return Task.CompletedTask;
                });
        }

        public async Task DisposeAsync()
        {
            if (_subscription is not null)
                await _subscription.DisposeAsync();
            if (_services is not null)
                await _services.DisposeAsync();
        }

        [Fact(DisplayName = "Iris EasyNetQ-wrapped message round-trips to a real EasyNetQ consumer on RabbitMQ")]
        public async Task Can_Consume_EasyNetQ_Message()
        {
            var messageType = typeof(IrisEasyNetQTestMessage);

            var request = MessageRequest.Create(
                messageType: messageType.Name,
                json: "{\"Red\":1,\"Green\":2,\"Blue\":3}",
                generateIrisHeaders: false,
                messageFullyQualifiedName: messageType.FullName,
                framework: "EasyNetQ",
                messageAssemblyName: messageType.Assembly.GetName().Name);

            request.WrapMessage(new EasyNetQAdapter());

            var managementPort = _rabbitMqContainer.GetMappedPublicPort(15672);
            var connection = await new RabbitMqConnector().ConnectAsync(new ConnectionData
            {
                ConnectionString = $"http://localhost:{managementPort}",
                Username = "guest",
                Password = "guest",
            }, false);
            connection.Should().NotBeNull();

            await connection!.SendAsync(new EndpointDetails
            {
                Provider = "rabbitmq",
                Address = $"http://localhost:{managementPort}",
                Name = QueueName,
                Type = "Queue",
            }, request);

            var completed = await Task.WhenAny(_received.Task, Task.Delay(TimeSpan.FromSeconds(30)));
            completed.Should().BeSameAs(_received.Task,
                "EasyNetQ should consume the message within 30s; a timeout means the AMQP type property did not reach the consumer");

            var message = await _received.Task;
            message.Should().Be(new IrisEasyNetQTestMessage(1, 2, 3));
        }
    }
}
