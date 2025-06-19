namespace Iris.Brokers
{
    public interface IBrokerConnectionManager
    {
        public Task<List<EndpointDetails>> GetEndpointsAsync();

        public Task AddConnectionAsync(IConnection connection);

        public List<IConnector> GetProviders();

        public Task<IConnection?> GetConnectionAsync(string address);

        public Task<List<IConnection>> GetConnectionsAsync();

        public Task<bool> RemoveConnectionAsync(string address);

        public Task<IConnection?> GetActiveConnectionAsync();
    }
}

