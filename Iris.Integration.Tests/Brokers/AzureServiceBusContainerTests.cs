using System;
using System.IO;
using System.Runtime.InteropServices;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using DotNet.Testcontainers.Networks;
using FluentAssertions;
using Iris.Brokers;
using Iris.Brokers.Azure;
using Iris.Brokers.Models;

namespace Iris.Integration.Tests.Brokers
{
    /// <summary>
    /// Emulator-backed tests for <see cref="AzureServiceBusConnection"/>.
    /// Uses Microsoft's Azure Service Bus emulator image
    /// (<c>mcr.microsoft.com/azure-messaging/servicebus-emulator</c>) which
    /// requires a sidecar SQL Edge container and a mounted Config.json that
    /// pre-declares queues (the emulator does not support dynamic queue
    /// creation via <see cref="ServiceBusAdministrationClient"/>).
    ///
    /// Pre-declared queues (see Resources/ServiceBusEmulatorConfig.json):
    /// - <c>iris-main-test</c>: MaxDeliveryCount=10, no expiration-DLQ. Used for peek/receive tests.
    /// - <c>iris-dlq-test</c>:  TTL=5s, DeadLetteringOnMessageExpiration=true. Used for DLQ tests.
    /// </summary>
    [Trait("Category", "Container")]
    public class AzureServiceBusContainerTests : IAsyncLifetime
    {
        // The Azure Service Bus emulator only loads queues/topics from a
        // mounted Config.json (it does NOT support dynamic queue creation
        // via ServiceBusAdministrationClient). The Dockerfile-baked image
        // approach works reliably on Linux but is intermittently flaky
        // inside Testcontainers .NET on macOS — the legacy Docker builder
        // hangs on the COPY layer or the emulator container fails to
        // expose port 5672 in time.
        //
        // On macOS, these tests auto-skip unless IRIS_RUN_ASB_TESTS=true
        // is set. On Linux (CI), they run unconditionally.
        //
        // To run locally on macOS:
        //   Option A: IRIS_RUN_ASB_TESTS=true dotnet test --filter "FullyQualifiedName~AzureServiceBusContainerTests"
        //   Option B: docker compose -f docker-compose.asb-emulator.yml up -d
        //             then run the tests with IRIS_RUN_ASB_TESTS=true
        private static bool ShouldSkipAsbTests =>
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            && Environment.GetEnvironmentVariable("IRIS_RUN_ASB_TESTS") != "true";

        private const string MainQueue = "iris-main-test";
        private const string DlqQueue = "iris-dlq-test";
        private const string SqlContainerAlias = "sqledge";
        private const string ServiceBusContainerAlias = "servicebus";
        private const string SqlPassword = "YourStrongPassword123!";
        private const ushort SqlPort = 1433;
        private const ushort ServiceBusPort = 5672;

        private readonly INetwork _network;
        private readonly IContainer _sqlContainer;
        private readonly IContainer _serviceBusContainer;
        private readonly IFutureDockerImage _serviceBusImage;

        // The SB emulator client uses fixed port 5672 (the standard
        // UseDevelopmentEmulator=true connection string assumes this).
        // We bind the host port 1:1, which is fine because no other AMQP
        // service is bound to 0.0.0.0:5672 in the dev environment.
        private const string ConnectionString =
            "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;" +
            "SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

        public AzureServiceBusContainerTests()
        {
            _network = new NetworkBuilder()
                .WithName($"sb-network-{Guid.NewGuid():N}")
                .Build();

            _sqlContainer = new ContainerBuilder()
                .WithImage("mcr.microsoft.com/azure-sql-edge:latest")
                .WithPortBinding(SqlPort, true)
                .WithEnvironment("ACCEPT_EULA", "Y")
                .WithEnvironment("MSSQL_SA_PASSWORD", SqlPassword)
                .WithNetwork(_network)
                .WithNetworkAliases(SqlContainerAlias)
                .Build();

            // The SB emulator only loads queues/topics from a
            // /ServiceBus_Emulator/ConfigFiles/Config.json file mounted
            // into the container — it does not support dynamic queue
            // creation via the admin client. We bake the file into a
            // derived image at test time so the container has the file
            // present from t=0; this is more reliable than bind/resource
            // mounts (which race with the emulator's startup script and
            // Docker Desktop's file sharing on macOS).
            var dockerfileDir = Path.Combine(
                AppContext.BaseDirectory, "Brokers", "Resources");

            _serviceBusImage = new ImageFromDockerfileBuilder()
                .WithName($"iris-sb-emulator-test:{Guid.NewGuid():N}")
                .WithDockerfileDirectory(dockerfileDir)
                .WithDockerfile("Dockerfile")
                .WithDeleteIfExists(true)
                .Build();

            _serviceBusContainer = new ContainerBuilder()
                .WithImage(_serviceBusImage)
                .WithPortBinding(ServiceBusPort, ServiceBusPort)
                .WithEnvironment("ACCEPT_EULA", "Y")
                .WithEnvironment("SQL_SERVER", SqlContainerAlias)
                .WithEnvironment("MSSQL_SA_PASSWORD", SqlPassword)
                .WithNetwork(_network)
                .WithNetworkAliases(ServiceBusContainerAlias)
                // The emulator image is minimal — no nc/bash — so the
                // default UntilPortIsAvailable wait strategy spins forever.
                // Wait for the explicit "Successfully Up" log line.
                // Even after that fires, we additionally poll the admin API
                // for queue existence in InitializeAsync — the emulator's
                // log line can race ahead of the AMQP listener's full
                // readiness on some runs.
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilMessageIsLogged("Emulator Service is Successfully Up"))
                .DependsOn(_sqlContainer)
                .Build();
        }

        public async Task InitializeAsync()
        {
            if (ShouldSkipAsbTests) return;

            await _network.CreateAsync();
            await _serviceBusImage.CreateAsync();
            await _sqlContainer.StartAsync();
            await _serviceBusContainer.StartAsync();

            // Poll the admin API until both pre-declared queues exist.
            // The emulator's "Successfully Up" log line can fire slightly
            // ahead of the AMQP listener's full readiness, producing
            // "MessagingEntityNotFound" if a test races ahead.
            var admin = new ServiceBusAdministrationClient(ConnectionString);
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    var mainExists = await admin.QueueExistsAsync(MainQueue);
                    var dlqExists = await admin.QueueExistsAsync(DlqQueue);
                    if (mainExists.Value && dlqExists.Value) return;
                }
                catch
                {
                    // Broker not ready yet — keep polling.
                }
                await Task.Delay(500);
            }
            throw new TimeoutException(
                "Service Bus emulator started but pre-declared queues did not appear within 60 seconds.");
        }

        public async Task DisposeAsync()
        {
            if (ShouldSkipAsbTests) return;

            await _serviceBusContainer.DisposeAsync();
            await _sqlContainer.DisposeAsync();
            await _serviceBusImage.DisposeAsync();
            await _network.DeleteAsync();
        }

        private AzureServiceBusConnection CreateConnection()
        {
            var metadata = new ConnectionMetadata
            {
                Connector = new AzureConnector(Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance),
                Address = ConnectionString,
            };
            var admin = new ServiceBusAdministrationClient(ConnectionString);
            var client = new ServiceBusClient(ConnectionString);
            return new AzureServiceBusConnection(metadata, admin, client);
        }

        private static EndpointDetails Endpoint(string queueName, string address) => new()
        {
            Provider = "AzureServiceBus",
            Address = address,
            Type = "Queue",
            Name = queueName,
        };

        [SkippableFact(DisplayName = "Can connect to Azure Service Bus emulator", Timeout = 300000)]
        public Task Can_Construct_Connection_From_Emulator()
        {
            Skip.If(ShouldSkipAsbTests, "ASB emulator tests are opt-in on macOS (set IRIS_RUN_ASB_TESTS=true)");

            var connection = CreateConnection();

            connection.Should().NotBeNull();
            connection.Address.Should().Be(ConnectionString);
            return Task.CompletedTask;
        }

        [SkippableFact(DisplayName = "Peek returns sent messages without removing them", Timeout = 300000)]
        public async Task Peek_is_non_destructive()
        {
            Skip.If(ShouldSkipAsbTests, "ASB emulator tests are opt-in on macOS (set IRIS_RUN_ASB_TESTS=true)");
            var connection = CreateConnection();
            await using var seeder = new ServiceBusClient(ConnectionString);
            await using var sender = seeder.CreateSender(MainQueue);

            // Drain in case previous tests left residue.
            var drainer = (IMessageReceiver)connection;
            await drainer.ReceiveAsync(Endpoint(MainQueue, ConnectionString), 50);

            await sender.SendMessageAsync(new ServiceBusMessage("{\"i\":1}"));
            await sender.SendMessageAsync(new ServiceBusMessage("{\"i\":2}"));
            await sender.SendMessageAsync(new ServiceBusMessage("{\"i\":3}"));

            var peeker = (IMessagePeeker)connection;
            var first = await peeker.PeekAsync(Endpoint(MainQueue, ConnectionString), 10);
            var second = await peeker.PeekAsync(Endpoint(MainQueue, ConnectionString), 10);

            first.Should().HaveCountGreaterThanOrEqualTo(3);
            second.Select(m => m.Body).Should().Contain(first.Select(m => m.Body));
        }

        [SkippableFact(DisplayName = "Receive removes messages from the main queue", Timeout = 300000)]
        public async Task Receive_is_destructive()
        {
            Skip.If(ShouldSkipAsbTests, "ASB emulator tests are opt-in on macOS (set IRIS_RUN_ASB_TESTS=true)");
            var connection = CreateConnection();
            await using var seeder = new ServiceBusClient(ConnectionString);
            await using var sender = seeder.CreateSender(MainQueue);

            // Drain first.
            var receiver = (IMessageReceiver)connection;
            await receiver.ReceiveAsync(Endpoint(MainQueue, ConnectionString), 50);

            await sender.SendMessageAsync(new ServiceBusMessage("{\"i\":1}"));
            await sender.SendMessageAsync(new ServiceBusMessage("{\"i\":2}"));
            await sender.SendMessageAsync(new ServiceBusMessage("{\"i\":3}"));

            // Give the broker a moment to make them visible.
            await Task.Delay(500);

            var received = await receiver.ReceiveAsync(Endpoint(MainQueue, ConnectionString), 10);
            received.Count.Should().BeGreaterThanOrEqualTo(3);

            var peeker = (IMessagePeeker)connection;
            var afterPeek = await peeker.PeekAsync(Endpoint(MainQueue, ConnectionString), 10);
            afterPeek.Should().BeEmpty();
        }

        [SkippableFact(DisplayName = "Dead-lettered messages are readable via ReceiveDeadLetterAsync", Timeout = 300000)]
        public async Task DeadLetter_receive_reads_from_dlq_subqueue()
        {
            Skip.If(ShouldSkipAsbTests, "ASB emulator tests are opt-in on macOS (set IRIS_RUN_ASB_TESTS=true)");
            var connection = CreateConnection();
            var dlqReceiver = (IDeadLetterReceiver)connection;

            // Drain the DLQ first in case prior tests left residue.
            await dlqReceiver.ReceiveDeadLetterAsync(Endpoint(DlqQueue, ConnectionString), 50);

            // Drive a message into the DLQ via MaxDeliveryCount — the
            // iris-dlq-test queue is configured with MaxDeliveryCount=1, so
            // after one abandon on a peek-locked receive the broker moves
            // the message to the DLQ sub-queue on next delivery.
            await using var seeder = new ServiceBusClient(ConnectionString);
            await using var sender = seeder.CreateSender(DlqQueue);
            await sender.SendMessageAsync(new ServiceBusMessage("{\"will\":\"dead-letter\"}"));

            // PeekLock receive → abandon (delivery 1) → the broker will move
            // the message to DLQ on the next delivery attempt because
            // MaxDeliveryCount has been exceeded.
            await using var peekLock = seeder.CreateReceiver(DlqQueue, new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock,
            });

            var attempt = await peekLock.ReceiveMessageAsync(TimeSpan.FromSeconds(10));
            attempt.Should().NotBeNull();
            await peekLock.AbandonMessageAsync(attempt!);

            // Second attempt: the broker dead-letters rather than delivers.
            // We wait briefly and then try another receive, which should
            // return null because the message has moved to the DLQ sub-queue.
            await Task.Delay(TimeSpan.FromSeconds(2));
            var secondAttempt = await peekLock.ReceiveMessageAsync(TimeSpan.FromSeconds(3));
            secondAttempt.Should().BeNull("the message should have moved to the DLQ sub-queue");

            var dlqMessages = await dlqReceiver.ReceiveDeadLetterAsync(Endpoint(DlqQueue, ConnectionString), 10);

            dlqMessages.Should().NotBeEmpty("the abandoned message should have been dead-lettered");
            dlqMessages.Should().AllSatisfy(m =>
            {
                m.Source.Should().Be(Iris.Brokers.Models.ReadSource.DeadLetter);
                m.Provider.Should().Be("AzureServiceBus");
            });
        }
    }
}
