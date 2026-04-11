using Iris.Contracts.Brokers.Models;
using Iris.Contracts.Results;
using static Iris.Contracts.Brokers.Endpoints.CreateConnection;
using static Iris.Contracts.Brokers.Endpoints.DeleteConnection;

namespace Iris.Components.Brokers
{
    public interface IBrokerService
    {
        Task<Result<CreateConnectionResponse>> CreateConnectionAsync(ConnectionData data);

        Task<Result<DeleteConnectionResponse>> DeleteConnectionAsync(string address);

        Task<List<EndpointDetails>> GetEndpointsAsync();
        
        Task<List<Provider>> GetProvidersAsync();

        Task<List<SupportedProvider>> GetSupportedProvidersAsync();

        Task RefreshDataAsync();

        /// <summary>
        /// Resolves a connection by its stable Guid Id (used by the
        /// /connections/{id} route). Returns null if no connection with that
        /// Id is currently registered.
        /// </summary>
        Task<Provider?> GetConnectionByIdAsync(Guid id);

        /// <summary>
        /// Returns the read capabilities of the broker at <paramref name="address"/>,
        /// built by probing which read interfaces the connection implements.
        /// Returns null if no connection exists at that address.
        /// </summary>
        Task<ReaderCapabilitiesDto?> GetReaderCapabilitiesAsync(string address);
    }
}