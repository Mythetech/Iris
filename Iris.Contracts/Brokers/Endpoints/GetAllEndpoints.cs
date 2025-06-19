using Iris.Contracts.Brokers.Models;

namespace Iris.Contracts.Brokers.Endpoints
{
    public class GetAllEndpoints
    {
        public record GetAllEndpointsRequest();

        public record GetAllEndpointsResponse(List<EndpointDetails> Endpoints);
    }
}

