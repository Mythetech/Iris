namespace Iris.Brokers
{
    public class BrokerConnectionManager : IBrokerConnectionManager
    {
        private readonly List<IConnector> _connectors;

        private List<IConnection> _connections = new();

        private IConnection? _active = default!;

        public BrokerConnectionManager(IEnumerable<IConnector> connectors)
        {
            _connectors = connectors.ToList();
        }

        public List<IConnector> Connectors => _connectors;

        public IConnection? Active => _active;

        public List<IConnection> Connections => _connections;

        public Task AddConnectionAsync(IConnection connection)
        {
            if (_connections.Any(x => x.Address.Equals(connection.Address, StringComparison.OrdinalIgnoreCase)))
                return Task.CompletedTask;

            _connections.Add(connection);
            _active = connection;
            return Task.CompletedTask;
        }

        public Task<IConnection?> GetActiveConnectionAsync()
        {
            return Task.FromResult(Active);
        }

        public Task<IConnection?> GetConnectionAsync(string address)
        {
            _active = _connections.FirstOrDefault(x => x.Address.Equals(address));
            return Task.FromResult(_active);
        }

        public Task<List<IConnection>> GetConnectionsAsync()
        {
            return Task.FromResult(Connections);
        }

        public async Task<List<EndpointDetails>> GetEndpointsAsync()
        {
            // Aggregate into a local: callers overlap, and a shared field would collect
            // every in-flight caller's endpoints into a single list. Snapshot the
            // connections too, because auto discovery can register one while this
            // aggregation is awaiting a broker.
            var endpoints = new List<EndpointDetails>();

            foreach (var connection in Connections.ToList())
            {
                endpoints.AddRange(await connection.GetEndpointsAsync());
            }

            return endpoints;
        }

        public List<IConnector> GetProviders()
        {
            return Connectors;
        }

        public Task<bool> RemoveConnectionAsync(string address)
        {
            var connection = _connections.FirstOrDefault(x => x.Address.Equals(address, StringComparison.OrdinalIgnoreCase));

            if (connection == null)
                return Task.FromResult(false);

            return Task.FromResult(_connections.Remove(connection));
        }
    }
}

