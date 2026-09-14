using FluentAssertions;
using Iris.Brokers;
using Iris.Brokers.Azure;
using Iris.Brokers.Frameworks;
using Iris.Brokers.Models;
using Iris.Integration.Tests.Fixtures;
using Microsoft.Extensions.Logging;
using NServiceBus;
using EndpointDetails = Iris.Brokers.EndpointDetails;

namespace Iris.Integration.Tests;

/// <summary>
/// Top-level, so its <c>FullName</c> plus assembly name is the value NServiceBus resolves
/// out of the <c>NServiceBus.EnclosedMessageTypes</c> header that
/// <see cref="NServiceBusAdapter"/> writes.
/// </summary>
public record IrisNServiceBusTestMessage(int Red, int Green, int Blue) : IMessage;

/// <summary>
/// The round-trip NServiceBus never had, which is why <c>VerifiedProviders</c> has been
/// empty since Wave 1.
///
/// <para>
/// Azure Queue Storage is the transport under test because it is the one the adapter was
/// written for: the body it emits is the Azure Storage Queues <c>MessageWrapper</c> shape,
/// a JSON envelope carrying a base64 payload and its headers inline. The RabbitMQ and SQS
/// transports read the same headers off AMQP properties and SQS message attributes
/// instead, so this test proving one provider says nothing about those two, and only this
/// one gets added to the verified set.
/// </para>
/// </summary>
[Collection("Azurite")]
[Trait("Category", "Container")]
public class NServiceBusTests : IAsyncLifetime
{
    private const string EndpointName = "irisnsbtest";

    private readonly string _connectionString;

    private IEndpointInstance? _endpoint;

    private static readonly TaskCompletionSource<IrisNServiceBusTestMessage> Received =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public NServiceBusTests(AzuriteContainerFixture fixture)
    {
        _connectionString = fixture.ConnectionString;
    }

    public async ValueTask InitializeAsync()
    {
        var configuration = new EndpointConfiguration(EndpointName);
        configuration.UseTransport(new AzureStorageQueueTransport(_connectionString));
        configuration.UseSerialization<SystemJsonSerializer>();

        // The endpoint has to create its own queues: the point of the test is that Iris can
        // write into a queue NServiceBus set up, not into one the test arranged to suit it.
        configuration.EnableInstallers();
        // Azure queue names allow no dots, and the transport does not sanitize this one.
        configuration.SendFailedMessagesTo($"{EndpointName}-error");

        _endpoint = await Endpoint.Start(configuration);
    }

    public async ValueTask DisposeAsync()
    {
        if (_endpoint is not null)
            await _endpoint.Stop();
    }

    [Fact(DisplayName = "Iris NServiceBus-wrapped message round-trips to a real NServiceBus endpoint on Azure Queue Storage", Timeout = 120000)]
    public async Task Can_Consume_NServiceBus_Message()
    {
        var ct = TestContext.Current.CancellationToken;
        var messageType = typeof(IrisNServiceBusTestMessage);

        // Wrapped exactly as LocalConnectionManager.SendMessageAsync does it, including the
        // assembly name, which is the half of EnclosedMessageTypes a consumer needs to
        // resolve the type at all.
        var request = MessageRequest.Create(
            messageType: messageType.Name,
            json: """{"Red":1,"Green":2,"Blue":3}""",
            generateIrisHeaders: false,
            messageFullyQualifiedName: messageType.FullName,
            framework: "NServiceBus",
            messageAssemblyName: messageType.Assembly.GetName().Name);

        request.WrapMessage(new NServiceBusAdapter());

        var connector = new AzureConnector(new LoggerFactory());
        var connection = await connector.ConnectAsync(
            new ConnectionData { ConnectionString = _connectionString }, ct, false);
        connection.Should().NotBeNull("AzureConnector must connect to Azurite");

        await connection!.SendAsync(
            new EndpointDetails
            {
                Provider = "azurequeuestorage",
                Address = _connectionString,
                Name = EndpointName,
                Type = "Queue",
            },
            request);

        var message = await Eventually.CompletesAsync(
            Received.Task,
            TimeSpan.FromSeconds(60),
            "NServiceBus consumes the Iris-wrapped envelope; a timeout means the adapter "
            + "produced a wire format the transport cannot decode or the serializer cannot bind",
            ct);

        message.Red.Should().Be(1);
        message.Green.Should().Be(2);
        message.Blue.Should().Be(3);
    }

    public class IrisNServiceBusTestHandler : IHandleMessages<IrisNServiceBusTestMessage>
    {
        public Task Handle(IrisNServiceBusTestMessage message, IMessageHandlerContext context)
        {
            Received.TrySetResult(message);
            return Task.CompletedTask;
        }
    }
}
