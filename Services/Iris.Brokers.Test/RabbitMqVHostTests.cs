using FluentAssertions;
using Iris.Brokers.Models;
using Iris.Brokers.RabbitMQ;
using Xunit;

namespace Iris.Brokers.Test;

/// <summary>
/// The vhost used to live on <see cref="RabbitMqConnector"/>, which is registered as a DI
/// singleton, and was derived as "the username, unless it is guest, in which case /". Two
/// separate bugs: connecting a second broker repointed the first connection's operations, and
/// a broker whose user is not guest had every operation aimed at a vhost named after the user.
/// </summary>
public class RabbitMqVHostTests
{
    private static ConnectionData Connection(string username, string? vhost = null) => new()
    {
        ConnectionString = "http://localhost:15672",
        Username = username,
        Password = "password",
        VHost = vhost,
    };

    [Theory(DisplayName = "A connection with no vhost given uses the broker default, whoever the user is")]
    [InlineData("guest")]
    [InlineData("admin")]
    [InlineData("")]
    public async Task Defaults_to_the_broker_default_vhost(string username)
    {
        var connection = await Connect(Connection(username));

        connection.VHost.Should().Be(RabbitMqConnector.DefaultVHost);
    }

    [Theory(DisplayName = "An explicit vhost is used exactly as given")]
    [InlineData("iris")]
    [InlineData("/")]
    [InlineData("my-vhost")]
    public async Task Uses_the_vhost_it_was_given(string vhost)
    {
        var connection = await Connect(Connection("admin", vhost));

        connection.VHost.Should().Be(vhost);
    }

    [Fact(DisplayName = "A blank vhost is the default, not an empty vhost name")]
    public async Task Treats_whitespace_as_unset()
    {
        var connection = await Connect(Connection("admin", "   "));

        connection.VHost.Should().Be(RabbitMqConnector.DefaultVHost);
    }

    [Fact(DisplayName = "Connecting a second broker leaves the first connection's vhost alone")]
    public async Task A_second_connection_does_not_repoint_the_first()
    {
        // One connector instance on purpose: it is a DI singleton, and this is the sequence
        // that used to silently move the first connection's sends onto the second's vhost.
        var connector = new RabbitMqConnector();

        var first = await Connect(Connection("admin", "orders"), connector);
        var second = await Connect(Connection("admin", "billing"), connector);

        first.VHost.Should().Be("orders");
        second.VHost.Should().Be("billing");
    }

    private static async Task<RabbitMqConnection> Connect(ConnectionData data, RabbitMqConnector? connector = null)
    {
        // discoverEndpoints: false keeps this off the network. Constructing the management
        // client opens nothing; only a call on it would.
        var connection = await (connector ?? new RabbitMqConnector())
            .ConnectAsync(data, discoverEndpoints: false);

        return connection.Should().BeOfType<RabbitMqConnection>().Subject;
    }
}
