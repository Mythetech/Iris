using Iris.Brokers;
using Iris.Contracts.Brokers.Events;
using Iris.Desktop.Brokers;
using Microsoft.Extensions.Logging;
using Mythetech.Framework.Infrastructure.Initialization;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Iris.Desktop.Infrastructure;

public class RestoreConnectionsInitializationHook : IAsyncInitializationHook
{
    private readonly ConnectionRepository _connectionRepository;
    private readonly IBrokerConnectionManager _connectionManager;
    private readonly IMessageBus _bus;
    private readonly ILogger<RestoreConnectionsInitializationHook> _logger;

    public RestoreConnectionsInitializationHook(
        ConnectionRepository connectionRepository,
        IBrokerConnectionManager connectionManager,
        IMessageBus bus,
        ILogger<RestoreConnectionsInitializationHook> logger)
    {
        _connectionRepository = connectionRepository;
        _connectionManager = connectionManager;
        _bus = bus;
        _logger = logger;
    }

    public int Order => 500;

    public string Name => "Restore Connections";

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var saved = _connectionRepository.GetAll();

        foreach (var entry in saved)
        {
            var connector = _connectionManager.GetProviders()
                .FirstOrDefault(x => x.Provider.Equals(entry.Provider, StringComparison.OrdinalIgnoreCase));

            if (connector == null)
            {
                _logger.LogWarning("No connector found for saved connection provider {Provider}, skipping", entry.Provider);
                continue;
            }

            try
            {
                var connection = await connector.ConnectAsync(entry.ToConnectionData());

                if (connection != null)
                {
                    await _connectionManager.AddConnectionAsync(connection);
                    await _bus.PublishAsync(new ConnectionCreated(connection.Connector.Provider, connection.Address));
                    _logger.LogInformation("Restored saved connection {Provider} at {Address}", entry.Provider, connection.Address);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to restore saved connection {Provider} ({Address}), skipping", entry.Provider, entry.Address);
            }
        }
    }
}
