using Iris.Brokers;
using Iris.Brokers.Extensions;
using Iris.Brokers.Models;
using Iris.Contracts.Brokers.Events;
using Microsoft.Extensions.Logging;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Iris.Desktop.Brokers;

public class AutoDiscovery
{
    private static readonly TimeSpan DiscoveryTimeout = TimeSpan.FromSeconds(5);

    private readonly IBrokerConnectionManager _connectionManager;
    private readonly IMessageBus _bus;
    private readonly ILogger<AutoDiscovery> _logger;

    public AutoDiscovery(IBrokerConnectionManager connectionManager, IMessageBus bus, ILogger<AutoDiscovery> logger)
    {
        _connectionManager = connectionManager;
        _bus = bus;
        _logger = logger;
    }

    public async Task<List<IConnection>> DiscoverLocalConnectionsAsync()
    {
        var rabbitProvider = _connectionManager.GetRabbitMq();
        var azureProvider = _connectionManager.GetAzure();

        try
        {
            var connection = await rabbitProvider.ConnectAsync(new ConnectionData()
            {
                ConnectionString = "http://127.0.0.1:15672/",
                Username = "guest",
                Password = "guest",
            }).WaitAsync(DiscoveryTimeout);

            if(connection != null)
            {
               await _connectionManager.AddConnectionAsync(connection);
               await _bus.PublishAsync(new ConnectionCreated(connection.Connector.Provider, connection.Address));
            }
        }
        catch (TimeoutException)
        {
            _logger.LogDebug("Local RabbitMQ discovery timed out after {Timeout}s", DiscoveryTimeout.TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Local RabbitMQ not available: {Message}", ex.Message);
        }

        try
        {
            var connection = await azureProvider.ConnectAsync(new ConnectionData()
            {
                ConnectionString = "UseDevelopmentStorage=true",
            }).WaitAsync(DiscoveryTimeout);

            if(connection != null)
            {
                await _connectionManager.AddConnectionAsync(connection);
                await _bus.PublishAsync(new ConnectionCreated(connection.Connector.Provider, connection.Address));
            }
        }
        catch (TimeoutException)
        {
            _logger.LogDebug("Local Azure storage emulator discovery timed out after {Timeout}s", DiscoveryTimeout.TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Local Azure storage emulator not available: {Message}", ex.Message);
        }
        
        var connections = await _connectionManager.GetConnectionsAsync();
        
        return connections;
    }
}