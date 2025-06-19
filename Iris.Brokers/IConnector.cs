using Iris.Brokers.Models;

namespace Iris.Brokers
{
    public interface IConnector
    {
        public string Provider { get; }

        public Task<IConnection?> ConnectAsync(ConnectionData data, bool discoverEndpoints = true);
    }
}

