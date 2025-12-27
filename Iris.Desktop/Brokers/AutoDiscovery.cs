using Iris.Brokers;
using Iris.Brokers.Extensions;
using Iris.Brokers.Models;
using Microsoft.Extensions.Logging;

namespace Iris.Desktop.Brokers;

public class AutoDiscovery
{
    private readonly IBrokerConnectionManager _connectionManager;
    private readonly ILogger<AutoDiscovery> _logger;

    public AutoDiscovery(IBrokerConnectionManager connectionManager, ILogger<AutoDiscovery> logger)
    {
        _connectionManager = connectionManager;
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
            });

            if(connection != null)
               await _connectionManager.AddConnectionAsync(connection);
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
            });

            if(connection != null)
                await _connectionManager.AddConnectionAsync(connection);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Local Azure storage emulator not available: {Message}", ex.Message);
        }
        
        var connections = await _connectionManager.GetConnectionsAsync();
        
        return connections;
    }
}