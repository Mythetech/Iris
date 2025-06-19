
using Iris.Contracts.Brokers.Models;

namespace Iris.Contracts.Integrations
{
    public class IntegrationMetadata
    {
        public required ConnectionData Data { get; set; }

        public required string Provider { get; set; }

        public string? Address { get; set; }
    }
}

