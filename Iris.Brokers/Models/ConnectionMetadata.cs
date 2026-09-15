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
        /// Address of the target connection
        /// </summary>
        public string Address { get; set; } = "";
    }
}

