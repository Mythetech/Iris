using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Iris.Brokers.Test;

public class BrokerConnectionManagerTests
{
    private readonly BrokerConnectionManager _sut;

    public BrokerConnectionManagerTests()
    {
        _sut = new BrokerConnectionManager(Enumerable.Empty<IConnector>());
    }

    [Fact]
    public async Task AddConnectionAsync_DuplicateAddress_DoesNotAddSecondConnection()
    {
        var first = CreateConnection("http://127.0.0.1:15672/");
        var second = CreateConnection("http://127.0.0.1:15672/");

        await _sut.AddConnectionAsync(first);
        await _sut.AddConnectionAsync(second);

        _sut.Connections.Should().HaveCount(1);
        _sut.Connections[0].Should().BeSameAs(first);
    }

    [Fact]
    public async Task AddConnectionAsync_DuplicateAddress_CaseInsensitive()
    {
        var first = CreateConnection("http://localhost:15672/");
        var second = CreateConnection("HTTP://LOCALHOST:15672/");

        await _sut.AddConnectionAsync(first);
        await _sut.AddConnectionAsync(second);

        _sut.Connections.Should().HaveCount(1);
    }

    [Fact]
    public async Task AddConnectionAsync_DifferentAddresses_AddsBoth()
    {
        var first = CreateConnection("http://127.0.0.1:15672/");
        var second = CreateConnection("UseDevelopmentStorage=true");

        await _sut.AddConnectionAsync(first);
        await _sut.AddConnectionAsync(second);

        _sut.Connections.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetEndpointsAsync_ConcurrentCalls_DoesNotDoubleCount()
    {
        // The gate holds the first connection's fetch open so the second caller starts
        // aggregating before the first has finished, as the panel and the page do.
        var gate = new TaskCompletionSource();
        var rabbit = CreateConnection("http://127.0.0.1:15672/", endpointCount: 3, gate: gate.Task);
        var azure = CreateConnection("iris-demo.servicebus.windows.net", "Azure", endpointCount: 2);

        await _sut.AddConnectionAsync(rabbit);
        await _sut.AddConnectionAsync(azure);

        var first = _sut.GetEndpointsAsync();
        var second = _sut.GetEndpointsAsync();

        gate.SetResult();

        var results = await Task.WhenAll(first, second);

        results[0].Should().HaveCount(5);
        results[1].Should().HaveCount(5);
    }

    [Fact]
    public async Task GetEndpointsAsync_ConnectionAddedMidAggregation_AggregatesTheConnectionsItStartedWith()
    {
        // Auto discovery registers connections while the panel is already loading endpoints,
        // so a connection can arrive part way through an aggregation.
        var gate = new TaskCompletionSource();
        var rabbit = CreateConnection("http://127.0.0.1:15672/", endpointCount: 3, gate: gate.Task);

        await _sut.AddConnectionAsync(rabbit);

        var pending = _sut.GetEndpointsAsync();

        await _sut.AddConnectionAsync(
            CreateConnection("iris-demo.servicebus.windows.net", "Azure", endpointCount: 2));

        gate.SetResult();

        var endpoints = await pending;

        endpoints.Should().HaveCount(3);
    }

    private static IConnection CreateConnection(
        string address, string provider = "RabbitMq", int endpointCount = 0, Task? gate = null)
    {
        var connection = Substitute.For<IConnection>();
        connection.Address.Returns(address);

        var endpoints = Enumerable.Range(0, endpointCount)
            .Select(i => new EndpointDetails
            {
                Address = address,
                Name = $"queue-{i}",
                Type = "Queue",
                Provider = provider,
            })
            .ToList();

        connection.GetEndpointsAsync().Returns(async _ =>
        {
            if (gate is not null)
                await gate;

            return endpoints;
        });

        return connection;
    }
}
