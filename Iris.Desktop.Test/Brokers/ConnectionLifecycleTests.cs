using FluentAssertions;
using Iris.Brokers;
using Iris.Brokers.Exceptions;
using Iris.Contracts.Brokers.Endpoints;
using Iris.Contracts.Brokers.Events;
using Iris.Contracts.Results;
using NSubstitute;
using ConnectionData = Iris.Contracts.Brokers.Models.ConnectionData;
using BrokerEndpoint = Iris.Brokers.EndpointDetails;

namespace Iris.Desktop.Test.Brokers;

/// <summary>
/// Creating and deleting connections, which is where the in-memory connection manager and
/// the saved copy on disk have to agree. They are written by two different objects, so the
/// cases that matter are the ones where one succeeds and the other should not run.
/// </summary>
public class ConnectionLifecycleTests
{
    private static ConnectionData Rabbit(string uri = "amqp://localhost") => new()
    {
        Provider = "RabbitMQ",
        Uri = uri,
        Username = "guest",
        Password = "guest",
    };

    private static void WithProvider(ConnectionManagerHarness harness, FakeConnector connector)
        => harness.Connections.GetProviders().Returns([connector]);

    [Fact(DisplayName = "A new connection is registered, announced and saved")]
    public async Task Creating_a_connection()
    {
        using var harness = new ConnectionManagerHarness();
        var connection = new RecordingConnection("RabbitMQ")
        {
            Address = "amqp://localhost",
            Endpoints = [new BrokerEndpoint { Address = "amqp://localhost", Name = "orders", Provider = "RabbitMQ", Type = "Queue" }],
        };
        WithProvider(harness, new FakeConnector("RabbitMQ") { Connection = connection });

        var result = await harness.Manager.CreateConnectionAsync(Rabbit());

        var response = result.Should().BeOfType<Success<CreateConnection.CreateConnectionResponse>>().Which.Value;
        response.Success.Should().BeTrue();
        response.Address.Should().Be("amqp://localhost");
        response.Endpoints.Should().ContainSingle().Which.Name.Should().Be("orders");

        await harness.Connections.Received(1).AddConnectionAsync(connection);
        harness.Published<ConnectionCreated>().Should().ContainSingle()
            .Which.Should().Be(new ConnectionCreated("RabbitMQ", "amqp://localhost"));
        harness.Repository.GetAll().Should().ContainSingle().Which.Address.Should().Be("amqp://localhost");
    }

    [Fact(DisplayName = "A provider name no connector answers to fails")]
    public async Task An_unknown_provider_fails()
    {
        using var harness = new ConnectionManagerHarness();
        WithProvider(harness, new FakeConnector("RabbitMQ"));

        var result = await harness.Manager.CreateConnectionAsync(new ConnectionData { Provider = "Kafka" });

        result.Should().BeOfType<Failure<CreateConnection.CreateConnectionResponse>>()
            .Which.Message.Should().Be("Provider not found");
        harness.Repository.GetAll().Should().BeEmpty();
    }

    [Fact(DisplayName = "Provider names are matched without regard to case")]
    public async Task Provider_matching_ignores_case()
    {
        using var harness = new ConnectionManagerHarness();
        var connection = new RecordingConnection("RabbitMQ") { Address = "amqp://localhost" };
        WithProvider(harness, new FakeConnector("RabbitMQ") { Connection = connection });

        var data = Rabbit();
        data.Provider = "rabbitmq";

        var result = await harness.Manager.CreateConnectionAsync(data);

        result.Should().BeOfType<Success<CreateConnection.CreateConnectionResponse>>();
    }

    [Fact(DisplayName = "Bad credentials surface the broker's own message, and save nothing")]
    public async Task Invalid_credentials_fail()
    {
        using var harness = new ConnectionManagerHarness();
        WithProvider(harness, new FakeConnector("RabbitMQ")
        {
            ConnectThrows = new InvalidConnectionException("ACCESS_REFUSED - Login was refused"),
        });

        var result = await harness.Manager.CreateConnectionAsync(Rabbit());

        result.Should().BeOfType<Failure<CreateConnection.CreateConnectionResponse>>()
            .Which.Message.Should().Be("ACCESS_REFUSED - Login was refused");

        // A connection that was never established must not leave a saved entry behind that
        // the next startup would try to restore.
        harness.Repository.GetAll().Should().BeEmpty();
        harness.Published<ConnectionCreated>().Should().BeEmpty();
    }

    [Fact(DisplayName = "A connector that declines without throwing still fails")]
    public async Task A_null_connection_fails()
    {
        using var harness = new ConnectionManagerHarness();
        WithProvider(harness, new FakeConnector("RabbitMQ") { Connection = null });

        var result = await harness.Manager.CreateConnectionAsync(Rabbit());

        result.Should().BeOfType<Failure<CreateConnection.CreateConnectionResponse>>()
            .Which.Message.Should().Be("Connection error");
        harness.Repository.GetAll().Should().BeEmpty();
    }

    [Fact(DisplayName = "The connector is handed the credentials the caller supplied")]
    public async Task Connection_data_reaches_the_connector()
    {
        using var harness = new ConnectionManagerHarness();
        var connector = new FakeConnector("RabbitMQ")
        {
            Connection = new RecordingConnection("RabbitMQ") { Address = "amqp://localhost" },
        };
        WithProvider(harness, connector);

        var data = Rabbit();
        data.VHost = "iris-tests";

        await harness.Manager.CreateConnectionAsync(data);

        connector.LastConnectionData!.Uri.Should().Be("amqp://localhost");
        connector.LastConnectionData.Username.Should().Be("guest");
        connector.LastConnectionData.Password.Should().Be("guest");
        connector.LastConnectionData.VHost.Should().Be("iris-tests");
    }

    [Fact(DisplayName = "Reconnecting to the same address replaces the saved entry rather than adding one")]
    public async Task Saving_the_same_address_twice_replaces_it()
    {
        using var harness = new ConnectionManagerHarness();
        WithProvider(harness, new FakeConnector("RabbitMQ")
        {
            Connection = new RecordingConnection("RabbitMQ") { Address = "amqp://localhost" },
        });

        await harness.Manager.CreateConnectionAsync(Rabbit());

        var updated = Rabbit();
        updated.Password = "changed";
        await harness.Manager.CreateConnectionAsync(updated);

        harness.Repository.GetAll().Should().ContainSingle();
    }

    [Fact(DisplayName = "Deleting a connection drops the saved copy and announces it")]
    public async Task Deleting_a_connection()
    {
        using var harness = new ConnectionManagerHarness();
        WithProvider(harness, new FakeConnector("RabbitMQ")
        {
            Connection = new RecordingConnection("RabbitMQ") { Address = "amqp://localhost" },
        });
        await harness.Manager.CreateConnectionAsync(Rabbit());
        harness.Connections.RemoveConnectionAsync("amqp://localhost").Returns(true);

        var result = await harness.Manager.DeleteConnectionAsync("amqp://localhost");

        result.Should().BeOfType<Success<DeleteConnection.DeleteConnectionResponse>>()
            .Which.Value.Success.Should().BeTrue();
        harness.Repository.GetAll().Should().BeEmpty();
        harness.Published<ConnectionDeleted>().Should().ContainSingle()
            .Which.Address.Should().Be("amqp://localhost");
    }

    [Fact(DisplayName = "A delete the manager refuses leaves the saved connection in place")]
    public async Task A_refused_delete_keeps_the_saved_copy()
    {
        using var harness = new ConnectionManagerHarness();
        WithProvider(harness, new FakeConnector("RabbitMQ")
        {
            Connection = new RecordingConnection("RabbitMQ") { Address = "amqp://localhost" },
        });
        await harness.Manager.CreateConnectionAsync(Rabbit());
        harness.Connections.RemoveConnectionAsync(Arg.Any<string>()).Returns(false);

        var result = await harness.Manager.DeleteConnectionAsync("amqp://localhost");

        result.Should().BeOfType<Failure<DeleteConnection.DeleteConnectionResponse>>()
            .Which.Message.Should().Be("Connection not found");

        // Deleting the saved copy anyway would silently lose the credentials for a
        // connection that is still live in this session.
        harness.Repository.GetAll().Should().ContainSingle();
        harness.Published<ConnectionDeleted>().Should().BeEmpty();
    }

    [Fact(DisplayName = "Live connections are projected with their transport and endpoint count")]
    public async Task Providers_are_projected()
    {
        using var harness = new ConnectionManagerHarness();
        var connection = new RecordingConnection("RabbitMQ")
        {
            Name = "amqp",
            Address = "amqp://localhost",
            EndpointCount = 4,
        };
        harness.Connections.GetConnectionsAsync().Returns(Task.FromResult<List<IConnection>>([connection]));

        var providers = await harness.Manager.GetProvidersAsync();

        var provider = providers.Should().ContainSingle().Subject;
        provider.Id.Should().Be(connection.Id);
        provider.Name.Should().Be("RabbitMQ");
        provider.Address.Should().Be("amqp://localhost");
        provider.Endpoints.Should().Be(4);
        provider.Transport.Should().Be("amqp");
    }

    [Fact(DisplayName = "A connection is findable by the id the UI routes on")]
    public async Task A_connection_is_found_by_id()
    {
        using var harness = new ConnectionManagerHarness();
        var connection = new RecordingConnection("RabbitMQ") { Address = "amqp://localhost" };
        harness.Connections.GetConnectionsAsync().Returns(Task.FromResult<List<IConnection>>([connection]));

        var found = await harness.Manager.GetConnectionByIdAsync(connection.Id);

        found.Should().NotBeNull();
        found!.Address.Should().Be("amqp://localhost");
    }

    [Fact(DisplayName = "An id that is not connected returns nothing rather than an empty provider")]
    public async Task An_unknown_id_returns_null()
    {
        using var harness = new ConnectionManagerHarness();
        harness.Connections.GetConnectionsAsync().Returns(Task.FromResult<List<IConnection>>([]));

        var found = await harness.Manager.GetConnectionByIdAsync(Guid.NewGuid());

        found.Should().BeNull();
    }

    [Fact(DisplayName = "Endpoints are projected across every live connection")]
    public async Task Endpoints_are_projected()
    {
        using var harness = new ConnectionManagerHarness();
        harness.Connections.GetEndpointsAsync().Returns(Task.FromResult<List<BrokerEndpoint>>(
        [
            new BrokerEndpoint { Address = "amqp://localhost", Name = "orders", Provider = "RabbitMQ", Type = "Queue" },
            new BrokerEndpoint { Address = "sb://emulator", Name = "events", Provider = "AzureServiceBus", Type = "Topic" },
        ]));

        var endpoints = await harness.Manager.GetEndpointsAsync();

        endpoints.Should().HaveCount(2);
        endpoints[0].Name.Should().Be("orders");
        endpoints[0].Type.Should().Be("Queue");
        endpoints[1].Provider.Should().Be("AzureServiceBus");
        endpoints[1].Type.Should().Be("Topic");
    }

    [Fact(DisplayName = "The supported provider list comes from the registered connectors")]
    public async Task Supported_providers_come_from_the_connectors()
    {
        using var harness = new ConnectionManagerHarness();
        harness.Connections.GetProviders().Returns([new FakeConnector("RabbitMQ"), new FakeConnector("AmazonSQS")]);

        var providers = await harness.Manager.GetSupportedProvidersAsync();

        providers.Select(p => p.Name).Should().Equal("RabbitMQ", "AmazonSQS");
    }
}
