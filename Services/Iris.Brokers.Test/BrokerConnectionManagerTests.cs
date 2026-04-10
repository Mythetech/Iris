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

    private static IConnection CreateConnection(string address)
    {
        var connection = Substitute.For<IConnection>();
        connection.Address.Returns(address);
        return connection;
    }
}
