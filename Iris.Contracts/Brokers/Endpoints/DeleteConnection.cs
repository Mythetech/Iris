namespace Iris.Contracts.Brokers.Endpoints
{
    public class DeleteConnection
    {
        public record DeleteConnectionRequest(string Address);

        public record DeleteConnectionResponse(bool Success);
    }
}

