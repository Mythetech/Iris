using Iris.Contracts.Brokers.Models;

namespace Iris.Contracts.Brokers.Endpoints
{
    public class CreateConnection
    {
        public record CreateConnectionRequest(ConnectionData Data, bool AutoDiscover, bool Save);

        public record CreateConnectionResponse(bool Success, string Address, List<EndpointDetails>? Endpoints);
    }
}

