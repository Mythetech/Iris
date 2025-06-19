using Iris.Brokers;
using Iris.Brokers.Extensions;
using Iris.Brokers.Models;

namespace Iris.Desktop.Brokers;

public class AutoDiscovery
{
    private readonly IBrokerConnectionManager _connectionManager;

    public AutoDiscovery(IBrokerConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
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
            Console.WriteLine(ex.Message);
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
            Console.WriteLine(ex.Message);
        }
        
        var connections = await _connectionManager.GetConnectionsAsync();
        
        return connections;
    }
}