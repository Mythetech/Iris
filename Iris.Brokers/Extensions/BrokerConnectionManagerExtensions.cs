using EasyNetQ.Management.Client.Model;
using Iris.Brokers.Amazon;
using Iris.Brokers.Azure;
using Iris.Brokers.Models;
using Iris.Brokers.RabbitMQ;

namespace Iris.Brokers.Extensions;

public static class BrokerConnectionManagerExtensions
{
    public static AzureConnector GetAzure(this IBrokerConnectionManager connectionManager)
        => (AzureConnector)connectionManager.GetProviders().First(x => x.Provider.Equals("azure", StringComparison.OrdinalIgnoreCase));
    
    public static AmazonWebServicesConnector GetAws(this IBrokerConnectionManager connectionManager)
        => (AmazonWebServicesConnector)connectionManager.GetProviders().First(x => x.Provider.Equals("amazon", StringComparison.OrdinalIgnoreCase));
    
    public static RabbitMqConnector GetRabbitMq(this IBrokerConnectionManager connectionManager)
        => (RabbitMqConnector)connectionManager.GetProviders().First(x => x.Provider.Equals("rabbitmq", StringComparison.OrdinalIgnoreCase));

    public static async Task<IConnection?> CreateConnectionAsync(this IBrokerConnectionManager connectionManager,
        string providerName,
        ConnectionData data)
    {
        var provider = connectionManager.GetProviders().First(x => x.Provider.Equals(providerName, StringComparison.OrdinalIgnoreCase));
        
        var connection = await provider.ConnectAsync(data);
        
        if(connection != null)
            await connectionManager.AddConnectionAsync(connection);
        
        return connection;
    }
}