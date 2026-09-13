using Bunit;
using FluentAssertions;
using Iris.Components.Home;
using Iris.Contracts.Brokers.Models;
using Xunit;

namespace Iris.Components.Test.Home;

public class ConnectionDoughnutTests : IrisTestContext
{
    private static List<EndpointDetails> ConnectionsAcross(int addresses) =>
        Enumerable.Range(0, addresses)
            .Select(i => new EndpointDetails
            {
                Name = $"queue-{i}",
                Address = $"amqp://broker-{i}",
                Provider = "rabbitmq",
                Type = "Queue",
            })
            .ToList();

    [Theory(DisplayName = "Renders a chip per connection past the end of the palette")]
    [InlineData(9)]
    // Ten is the first count the nine-colour palette cannot serve, and it threw
    // IndexOutOfRangeException from Home rather than reusing a colour.
    [InlineData(10)]
    [InlineData(25)]
    public void Survives_more_connections_than_colours(int connections)
    {
        var cut = RenderComponent<ConnectionDoughnut>(p => p
            .Add(x => x.Connections, ConnectionsAcross(connections)));

        cut.FindAll("button.mud-chip").Should().HaveCount(connections);
    }
}
