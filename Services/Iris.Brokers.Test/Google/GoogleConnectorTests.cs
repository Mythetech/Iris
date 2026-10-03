using FluentAssertions;
using Iris.Brokers.Exceptions;
using Iris.Brokers.Google;
using Iris.Brokers.Models;
using Xunit;

namespace Iris.Brokers.Test.Google;

/// <summary>
/// What the connector does before and around its first call. Port 1 is used as a host that
/// refuses connections at once, so these need no emulator and return in well under a second.
/// </summary>
public class GoogleConnectorTests
{
    [Fact]
    public void The_provider_is_google()
    {
        new GoogleConnector().Provider.Should().Be("Google");
    }

    [Fact]
    public async Task Connecting_to_a_port_nothing_listens_on_names_the_emulator_host()
    {
        var act = () => new GoogleConnector().ConnectAsync(
            new ConnectionData { Uri = "localhost:1", ProjectId = "iris" }, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<InvalidConnectionException>())
            .WithMessage("Nothing answered at localhost:1. Is the Pub/Sub emulator running?");
    }

    [Fact]
    public async Task Connecting_without_discovery_still_calls_the_broker()
    {
        var act = () => new GoogleConnector().ConnectAsync(
            new ConnectionData { Uri = "localhost:1", ProjectId = "iris" },
            TestContext.Current.CancellationToken,
            discoverEndpoints: false);

        (await act.Should().ThrowAsync<InvalidConnectionException>())
            .WithMessage("Nothing answered at localhost:1. Is the Pub/Sub emulator running?");
    }

    [Theory]
    [InlineData("localhost:99999")]
    [InlineData("localhost:8085:8085")]
    public async Task A_host_the_network_stack_would_reject_is_refused_as_a_bad_host(string host)
    {
        var act = () => new GoogleConnector().ConnectAsync(
            new ConnectionData { Uri = host, ProjectId = "iris" }, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<InvalidConnectionException>()).WithMessage(PubSubErrors.EmulatorHostInvalid);
    }

    [Fact]
    public async Task Anything_unexpected_during_connect_still_ends_as_a_connection_failure_the_dialog_can_show()
    {
        // The Add Connection dialog only handles InvalidConnectionException. Any other type
        // leaves it stuck on "Connecting".
        var act = () => new GoogleConnector().ConnectAsync(
            new ConnectionData { Uri = "localhost:1", ProjectId = "not/a/project" }, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<InvalidConnectionException>()).WithMessage(PubSubErrors.ConnectFailed);
    }

    [Fact]
    public async Task A_connect_that_cannot_resolve_its_settings_fails_before_building_a_client()
    {
        var act = () => new GoogleConnector().ConnectAsync(new ConnectionData(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<InvalidConnectionException>()).WithMessage(PubSubErrors.ProjectIdRequired);
    }
}
