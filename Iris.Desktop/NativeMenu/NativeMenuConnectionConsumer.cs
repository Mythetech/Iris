using Iris.Brokers;
using Iris.Components.NativeMenu;
using Iris.Contracts.Brokers.Events;
using Iris.Contracts.Brokers.Models;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Iris.Desktop.NativeMenu;

public class NativeMenuConnectionConsumer : IConsumer<ConnectionCreated>, IConsumer<ConnectionDeleted>
{
    private readonly INativeMenuService _menuService;
    private readonly IBrokerConnectionManager _connectionManager;

    public NativeMenuConnectionConsumer(INativeMenuService menuService, IBrokerConnectionManager connectionManager)
    {
        _menuService = menuService;
        _connectionManager = connectionManager;
    }

    public async Task Consume(ConnectionCreated message)
    {
        await RebuildMenu();
    }

    public async Task Consume(ConnectionDeleted message)
    {
        await RebuildMenu();
    }

    private async Task RebuildMenu()
    {
        if (!_menuService.IsActive)
            return;

        var connections = await _connectionManager.GetConnectionsAsync();
        var providers = connections.Select(c => new Provider
        {
            Id = c.Id,
            Name = c.Connector.Provider,
            Address = c.Address,
            Endpoints = c.EndpointCount,
            Transport = c.Name
        }).ToList();

        _menuService.RebuildConnectionsMenu(providers);
    }
}
