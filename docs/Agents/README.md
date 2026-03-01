# AI Agent Development Guide for Iris

Context and guidance for AI coding assistants working on the Iris codebase.

## Project Identity

**Iris** is a desktop application for testing distributed systems — "Postman for message brokers." Developers visually connect to message brokers (RabbitMQ, Azure Service Bus, AWS SQS) and send/receive messages without writing code.

## Technology Stack

| Layer | Technology |
|-------|------------|
| **Runtime** | .NET 10, C# 14 |
| **Desktop** | Hermes (cross-platform native window + WebView) |
| **UI** | Blazor + MudBlazor 8 |
| **Infrastructure** | Mythetech Framework (message bus, settings, desktop services) |
| **Local Storage** | LiteDB (embedded NoSQL) |
| **Package Management** | Central package versions in `Directory.Packages.props` |
| **Versioning** | Nerdbank.GitVersioning |
| **Distribution** | Velopack |

## Architecture Quick Reference

### Project Dependency Graph

```
Iris.Desktop (main app)
├── Iris.Components (UI components)
│   ├── Iris.Contracts (shared DTOs)
│   └── Mythetech.Framework (message bus, settings)
├── Iris.Brokers (broker connections)
│   └── Iris.Contracts
├── Mythetech.Framework.Desktop (desktop services, settings storage)
├── Mythetech.Hermes.Blazor (cross-platform window + WebView)
└── Services/
    ├── Iris.Assemblies (dynamic type loading)
    ├── Iris.History (message history)
    ├── Iris.Brokers.Frameworks (MassTransit/NServiceBus/etc. adapters)
    └── Iris.Templates (saved templates)
```

### Key Interfaces

| Interface | Purpose | Location |
|-----------|---------|----------|
| `IConnector` | Creates broker connections | `Iris.Brokers/IConnector.cs` |
| `IConnection` | Represents an active broker connection | `Iris.Brokers/IConnection.cs` |
| `IFramework` | Wraps messages for frameworks (MassTransit, etc.) | `Services/Iris.Brokers.Frameworks/IFramework.cs` |
| `IBrokerService` | UI service for broker operations | `Iris.Components/Brokers/IBrokerService.cs` |
| `IMessageBus` | Internal pub/sub for component communication | `Mythetech.Framework` (NuGet) |

### Infrastructure (Mythetech Framework)

Iris delegates infrastructure concerns to the framework:

- **Message Bus** — `IMessageBus` / `IConsumer<T>` for internal component pub/sub. Registered via `AddMessageBus()` / `UseMessageBus()`.
- **Settings** — `SettingsBase` subclasses with `[Setting]` attributes, rendered by `<SettingsPanel />`. Registered via `AddSettingsFramework()` / `RegisterSettingsFromAssembly()`.
- **Desktop Services** — Settings persistence via LiteDB, registered with `AddDesktopSettingsStorage("Iris")` and `AddDesktopServices(DesktopHost.Hermes)`.

Settings classes in this project:
- `Iris.Components/Messaging/MessagingSettings.cs` — SendIrisHeader toggle, layout settings
- `Iris.Desktop/History/HistorySettings.cs` — History management panel

## Common Tasks

### Adding a New Message Broker

1. Create folder: `Iris.Brokers/NewBroker/`
2. Implement `IConnector`:
   ```csharp
   public class NewBrokerConnector : IConnector
   {
       public string Provider => "newbroker";
       public Task<IConnection?> ConnectAsync(ConnectionData data, bool discoverEndpoints = true);
   }
   ```
3. Implement `IConnection`:
   ```csharp
   public class NewBrokerConnection : IConnection
   {
       public Task<List<EndpointDetails>> GetEndpointsAsync();
       public Task SendAsync(EndpointDetails endpoint, string json);
   }
   ```
4. The connector is auto-registered via assembly scanning in `Program.cs`

### Adding a New Framework Adapter

1. Create adapter in `Services/Iris.Brokers.Frameworks/`:
   ```csharp
   public class NewFrameworkAdapter : IFramework
   {
       public string Name => "NewFramework";
       public string CreateWrappedMessage(IMessageRequest request);
   }
   ```
2. Register in `StaticFrameworkProvider`

### Adding a Settings Section

1. Create a `SettingsBase` subclass:
   ```csharp
   public class MySettings : SettingsBase
   {
       public override string SettingsId => "MyFeature";
       public override string DisplayName => "My Feature";
       public override string Icon => Icons.Material.Filled.Settings;
       public override int Order => 30;

       [Setting(Label = "Enable Feature", Description = "...")]
       public bool Enabled { get; set; }
   }
   ```
2. It will be auto-discovered by `RegisterSettingsFromAssembly()`

### Adding a New UI Component

1. Add to appropriate folder in `Iris.Components/`
2. Inherit from `IrisBaseComponent` for common properties (Id, Class, Style)
3. Use `IrisPageBase` for full pages (provides navigation, snackbar, result handling)
4. Write tests in `Iris.Components.Test/` using `IrisTestContext`

## Code Conventions

### DO

- Use `ILogger<T>` for all logging
- Use central package versions from `Directory.Packages.props`
- Use `_` prefix for private fields
- Follow async/await patterns throughout
- Use `Result<T>` for operation results with error information
- Keep UI logic in components, business logic in services

### DON'T

- Use `Console.WriteLine` in production code
- Hardcode secrets or connection strings
- Add inline comments explaining obvious code
- Create unnecessary abstractions for one-time operations
- Mix sync and async patterns (use async where available)

## Testing Patterns

### Component Testing with bUnit

```csharp
public class MyTests : IrisTestContext
{
    [Fact(DisplayName = "Component renders correctly")]
    public void Component_Renders()
    {
        // IrisTestContext sets JSInterop.Mode = JSRuntimeMode.Loose
        // and registers MudBlazor services
        var cut = RenderComponent<MyComponent>();
        cut.MarkupMatches("<expected-markup />");
    }
}
```

### Integration Testing with Testcontainers

```csharp
public class BrokerTests : IAsyncLifetime
{
    private readonly RabbitMqContainer _container = new RabbitMqBuilder().Build();

    public Task InitializeAsync() => _container.StartAsync();
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
```

## File Locations

| What | Where |
|------|-------|
| Desktop entry point | `Iris.Desktop/Program.cs` |
| Component DI registration | `Iris.Components/IrisComponentRegistrationExtensions.cs` |
| Broker connectors | `Iris.Brokers/{Provider}/` |
| Framework adapters | `Services/Iris.Brokers.Frameworks/` |
| Shared UI components | `Iris.Components/Shared/` |
| Feature components | `Iris.Components/{Feature}/` |
| Local DB context | `Iris.Desktop/Infrastructure/IrisLiteDbContext.cs` |
| Theme/styling | `Iris.Components/Theme/` |
| Settings classes | `Iris.Components/Messaging/MessagingSettings.cs`, `Iris.Desktop/History/HistorySettings.cs` |

## Known Patterns to Preserve

1. **Auto-discovery of local connections** — `AutoDiscovery.cs` tries to connect to local RabbitMQ and Azure Storage Emulator on startup
2. **Framework message wrapping** — Messages can be wrapped in MassTransit/NServiceBus envelopes via `IFramework`
3. **Internal message bus** — Components communicate via `IMessageBus` pub/sub (from Mythetech Framework), not direct references
4. **Dynamic type generation** — `CodeGenerator` uses `System.Reflection.Emit` to create runtime types from loaded assemblies

## Areas Needing Future Work

1. **Project Consolidation** — Currently ~14 projects; could be simplified to ~5
2. **Connection Persistence** — Connections are currently in-memory only
3. **Template Persistence** — `LocalTemplateService` needs LiteDB-backed storage
4. **Retry/Resilience** — `Microsoft.Extensions.Resilience` is available but not wired up
5. **Cancellation Tokens** — Many async methods don't accept `CancellationToken`

## Debugging Tips

1. **Broker connection issues** — Check `AutoDiscovery.cs` for local connection logic
2. **Message not sending** — Verify `LocalConnectionManager.SendMessageAsync()` flow
3. **UI not updating** — Check if `StateHasChanged()` is being called, or if using `IMessageBus`
4. **Template/History issues** — Check `IrisLiteDbContext` and repository implementations
5. **Settings not persisting** — Verify `AddDesktopSettingsStorage("Iris")` is registered and `LoadPersistedSettingsAsync()` is called at startup
