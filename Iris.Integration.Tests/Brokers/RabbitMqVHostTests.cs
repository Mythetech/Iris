using EasyNetQ.Management.Client;
using EasyNetQ.Management.Client.Model;
using FluentAssertions;
using Iris.Brokers;
using Iris.Brokers.Models;
using Iris.Brokers.RabbitMQ;
using Iris.Integration.Tests.Fixtures;
using Testcontainers.RabbitMq;

namespace Iris.Integration.Tests.Brokers
{
    /// <summary>
    /// Every other RabbitMQ test connects as guest on the default vhost, which is exactly the
    /// one combination the old "vhost is the username, unless it is guest" guess happened to get
    /// right. These run as a named user on a named vhost, where that guess aimed every
    /// operation at a vhost that does not exist.
    /// </summary>
    [Collection("RabbitMQ")]
    [Trait("Category", TestCategories.Container)]
    public class RabbitMqVHostTests
    {
        private const string VHostName = "iris-vhost";
        private const string UserName = "iris-app";
        private const string Password = "iris-app-password";

        private readonly RabbitMqContainer _rabbitMqContainer;

        public RabbitMqVHostTests(RabbitMqContainerFixture fixture)
        {
            _rabbitMqContainer = fixture.Container;
        }

        private string ManagementUrl => $"http://localhost:{_rabbitMqContainer.GetMappedPublicPort(15672)}";

        [Fact(DisplayName = "A named user on a named vhost can send and read its own queue", Timeout = 120000)]
        public async Task Sends_and_reads_on_a_named_vhost()
        {
            var queueName = $"iris-vhost-send-{Guid.NewGuid():N}";
            await SeedAsync(queueName);

            var connection = await ConnectAsync(VHostName, TestContext.Current.CancellationToken);

            await connection.SendAsync(
                new EndpointDetails
                {
                    Provider = "rabbitmq",
                    Address = ManagementUrl,
                    Type = "queue",
                    Name = queueName,
                },
                MessageRequest.Create(queueName, "{\"hello\":\"vhost\"}", generateIrisHeaders: false));

            var peeker = connection.Should().BeAssignableTo<IMessagePeeker>().Subject;

            var messages = await Retry(() => peeker.PeekAsync(
                new EndpointDetails { Provider = "rabbitmq", Address = ManagementUrl, Type = "queue", Name = queueName },
                count: 10), TestContext.Current.CancellationToken);

            messages.Should().ContainSingle().Which.Body.Should().Contain("vhost");
        }

        [Fact(DisplayName = "Endpoint discovery lists only the connection's own vhost", Timeout = 120000)]
        public async Task Discovers_only_its_own_vhost()
        {
            var scopedQueue = $"iris-vhost-scoped-{Guid.NewGuid():N}";
            var defaultQueue = $"iris-default-scoped-{Guid.NewGuid():N}";

            await SeedAsync(scopedQueue);
            await CreateQueueAsync(RabbitMqConnector.DefaultVHost, defaultQueue);

            var connection = await ConnectAsync(VHostName, TestContext.Current.CancellationToken);

            var endpoints = await connection.GetEndpointsAsync();
            var names = endpoints.Select(e => e.Name).ToList();

            names.Should().Contain(scopedQueue);

            // Listing every vhost showed queues that no send, read or inspect on this
            // connection could touch.
            names.Should().NotContain(defaultQueue);
        }

        [Fact(DisplayName = "A named user with no vhost given still lands on the default vhost", Timeout = 120000)]
        public async Task Defaults_to_the_broker_default_rather_than_the_username()
        {
            var queueName = $"iris-default-{Guid.NewGuid():N}";
            await SeedAsync(queueName);
            await CreateQueueAsync(RabbitMqConnector.DefaultVHost, queueName);

            // The old guess turned user iris-app into vhost "iris-app", which does not exist, so
            // this send 404'd. There is no vhost named for the user here, on purpose.
            var connection = await ConnectAsync(vhost: null, TestContext.Current.CancellationToken);

            var send = async () => await connection.SendAsync(
                new EndpointDetails
                {
                    Provider = "rabbitmq",
                    Address = ManagementUrl,
                    Type = "queue",
                    Name = queueName,
                },
                MessageRequest.Create(queueName, "{\"hello\":\"default\"}", generateIrisHeaders: false));

            await send.Should().NotThrowAsync();
        }

        private async Task<IConnection> ConnectAsync(
            string? vhost,
            CancellationToken cancellationToken,
            string username = UserName,
            string password = Password)
        {
            var connection = await new RabbitMqConnector().ConnectAsync(
                new ConnectionData
                {
                    ConnectionString = ManagementUrl,
                    Username = username,
                    Password = password,
                    VHost = vhost,
                },
                cancellationToken,
                discoverEndpoints: false);

            connection.Should().NotBeNull();
            return connection!;
        }

        /// <summary>
        /// Creates the vhost, the user and the user's permissions on it, then the queue.
        /// rabbitmqctl rather than the management API because it is the same tool an operator
        /// would use and its arguments have not changed in a decade. Every step is idempotent,
        /// so each test in this class can call it.
        /// </summary>
        private async Task SeedAsync(string queueName)
        {
            await ExecAsync("rabbitmqctl", "add_vhost", VHostName);
            await ExecAsync("rabbitmqctl", "add_user", UserName, Password);
            await ExecAsync("rabbitmqctl", "set_user_tags", UserName, "management");
            await ExecAsync("rabbitmqctl", "set_permissions", "-p", VHostName, UserName, ".*", ".*", ".*");

            // guest is an administrator but its permissions are scoped to "/", so seeding a
            // queue on a new vhost over the management API is a 401 without this.
            await ExecAsync("rabbitmqctl", "set_permissions", "-p", VHostName, "guest", ".*", ".*", ".*");

            // And on the default vhost, so the "no vhost given" case can run as this user too.
            await ExecAsync("rabbitmqctl", "set_permissions", "-p", RabbitMqConnector.DefaultVHost, UserName, ".*", ".*", ".*");

            await CreateQueueAsync(VHostName, queueName);
        }

        /// <summary>
        /// Exit codes are ignored on purpose: "already exists" is the expected outcome for every
        /// call after the first, and a step that genuinely failed shows up as the test failing
        /// on the operation it was setting up.
        /// </summary>
        private async Task ExecAsync(params string[] command)
            => await _rabbitMqContainer.ExecAsync(command);

        private async Task CreateQueueAsync(string vhostName, string queueName)
        {
            var admin = Admin();
            var vhost = await admin.GetVhostAsync(vhostName);
            await admin.CreateQueueAsync(vhost, queueName,
                new QueueInfo(AutoDelete: false, Durable: true, Arguments: null!));
        }

        private ManagementClient Admin() => new(new Uri(ManagementUrl), "guest", "guest");

        /// <summary>
        /// The management API's get-messages endpoint reads what has already been routed, and
        /// publishing is asynchronous, so the first read can legitimately come back empty.
        /// Bounded by the test's own timeout rather than by a fixed attempt count, and it
        /// now fails saying what it waited for instead of quietly returning nothing.
        /// </summary>
        private static Task<IReadOnlyList<ReceivedMessage>> Retry(
            Func<Task<IReadOnlyList<ReceivedMessage>>> read,
            CancellationToken cancellationToken)
            => Eventually.NonEmptyAsync(
                _ => read(),
                "the published message is readable from the queue",
                cancellationToken,
                interval: TimeSpan.FromMilliseconds(250));
    }
}
