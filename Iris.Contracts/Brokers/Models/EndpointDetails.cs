namespace Iris.Contracts.Brokers.Models
{
    public class EndpointDetails
    {
        /// <summary>
        /// Address where the endpoint exists
        /// </summary>
        public required string Address { get; set; }

        /// <summary>
        /// Name of the connection provider
        /// </summary>
        public required string Provider { get; set; }

        /// <summary>
        /// Name of the endpoint
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Specific type of endpoint, i.e. queue, topic
        /// </summary>
        public string? Type { get; set; }

        public override string ToString()
        {
            return Name;
        }

    }
}

