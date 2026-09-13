using System;
using System.Threading.Tasks;
using FluentAssertions;
using Iris.Brokers;
using Iris.Brokers.Frameworks;
using Iris.Brokers.Models;
using Iris.Brokers.RabbitMQ;
using Iris.Integration.Tests.Fixtures;
using Microsoft.Extensions.Hosting;
using Testcontainers.RabbitMq;
using Wolverine;
using Wolverine.RabbitMQ;

namespace Iris.Integration.Tests
{
    public record IrisWolverineTestMessage(int Red, int Green, int Blue);

    // Wolverine discovers static Handle methods; the completion source is static so the
    // test can observe a handler it does not construct.
    public static class IrisWolverineTestHandler
    {
        public static readonly TaskCompletionSource<IrisWolverineTestMessage> Received =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public static void Handle(IrisWolverineTestMessage message) => Received.TrySetResult(message);
    }

    [Collection("RabbitMQ")]
    [Trait("Category", "Container")]
    public class WolverineTests : IAsyncLifetime
    {
        private const string QueueName = "iris-wolverine-test";

        private readonly RabbitMqContainer _rabbitMqContainer;
        private IHost? _host;

        public WolverineTests(RabbitMqContainerFixture fixture)
        {
            _rabbitMqContainer = fixture.Container;
        }

        public async Task InitializeAsync()
        {
            var amqpPort = _rabbitMqContainer.GetMappedPublicPort(5672);

            _host = await Host.CreateDefaultBuilder()
                .UseWolverine(opts =>
                {
                    opts.ApplicationAssembly = typeof(WolverineTests).Assembly;
                    opts.Discovery.IncludeType(typeof(IrisWolverineTestHandler));
                    opts.UseRabbitMq(new Uri($"amqp://guest:guest@localhost:{amqpPort}"))
                        .AutoProvision()
                        .DisableDeadLetterQueueing();
                    opts.ListenToRabbitQueue(QueueName);
                })
                .StartAsync();
        }

        public async Task DisposeAsync()
        {
            if (_host is not null)
            {
                await _host.StopAsync();
                _host.Dispose();
            }
        }

        [Fact(DisplayName = "Iris Wolverine-wrapped message round-trips to a real Wolverine handler on RabbitMQ")]
        public async Task Can_Consume_Wolverine_Message()
        {
            var request = MessageRequest.Create(
                messageType: nameof(IrisWolverineTestMessage),
                json: "{\"Red\":1,\"Green\":2,\"Blue\":3}",
                generateIrisHeaders: false,
                messageFullyQualifiedName: typeof(IrisWolverineTestMessage).FullName,
                framework: "Wolverine");

            request.WrapMessage(new WolverineAdapter());

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

            var completed = await Task.WhenAny(IrisWolverineTestHandler.Received.Task, Task.Delay(TimeSpan.FromSeconds(30)));
            completed.Should().BeSameAs(IrisWolverineTestHandler.Received.Task,
                "Wolverine should handle the message within 30s; a timeout means the identity in the AMQP type property did not match the handler");

            var message = await IrisWolverineTestHandler.Received.Task;
            message.Should().Be(new IrisWolverineTestMessage(1, 2, 3));
        }
    }
}
