namespace Iris.Brokers.Models
{
    /// <summary>
    /// Metadata generated during the creation of a new connection. Encapsulates all async information a connection needs for its constructor
    /// </summary>
    public class ConnectionMetadata
    {
        /// <summary>
        /// The broker connector that created the connection
        /// </summary>
        public IConnector Connector { get; set; } = default!;

        /// <summary>
        /// The endpoint count the connector discovered pre initialization
        /// </summary>
        public int DiscoveredEndpoints { get; set; } = 0;

        /// <summary>
        /// The endpoints converted the connector discovered
        /// </summary>
        public List<EndpointDetails>? DiscoveredDetails { get; set; }

        /// <summary>
        /// Address of the target connection
        /// </summary>
        public string Address { get; set; } = "";

        /// <summary>
        /// When the connection is created
        /// </summary>
        public DateTimeOffset Created { get; set; } = DateTimeOffset.Now;
    }
}

