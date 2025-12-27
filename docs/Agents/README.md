# AI Agent Development Guide for Iris

This document provides context and guidance for AI coding assistants working on the Iris codebase. It captures institutional knowledge to help future AI agents (and developers) understand the project quickly.

## Project Identity

**Iris** is a desktop application for testing distributed systems - think "Postman for message brokers." It allows developers to visually connect to message brokers (RabbitMQ, Azure Service Bus, AWS SQS) and send/receive messages without writing code.

## Technology Stack

| Layer | Technology |
|-------|------------|
| **Framework** | .NET 9, C# 12 |
| **Desktop** | Photino.Blazor (cross-platform native window) |
| **UI** | Blazor + MudBlazor components |
| **Local Storage** | LiteDB (embedded NoSQL) |
| **Package Management** | Central package versions in `Directory.Packages.props` |
| **Versioning** | Nerdbank.GitVersioning |
| **Distribution** | Velopack |

## Architecture Quick Reference

### Key Design Patterns

1. **CQRS** - Commands and queries are separated. See `/docs/Architecture/CQRS.md`
2. **DDD** - Domain entities encapsulate behavior. See `/docs/Architecture/DDD.md`
3. **REPR** - Request-Endpoint-Response for API endpoints. See `/docs/Architecture/REPR.md`
4. **Strategy Pattern** - Broker connectors and framework adapters

### Project Dependency Graph

```
Iris.Desktop (main app)
├── Iris.Components (UI components)
│   └── Iris.Contracts (shared DTOs)
├── Iris.Brokers (broker connections)
│   └── Iris.Contracts
└── Services/
    ├── Iris.Assemblies (dynamic type loading)
    ├── Iris.History (message history)
    └── Iris.Templates (saved templates)
```

### Key Interfaces

| Interface | Purpose | Location |
|-----------|---------|----------|
| `IConnector` | Creates broker connections | `Iris.Brokers/IConnector.cs` |
| `IConnection` | Represents an active broker connection | `Iris.Brokers/IConnection.cs` |
| `IFramework` | Wraps messages for frameworks (MassTransit, etc.) | `Iris.Brokers/Frameworks/IFramework.cs` |
| `IBrokerService` | UI service for broker operations | `Iris.Components/Brokers/IBrokerService.cs` |
| `IMessageBus` | Internal pub/sub for component communication | `Iris.Components/Infrastructure/MessageBus/IMessageBus.cs` |

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

1. Create adapter in `Iris.Brokers/Frameworks/`:
   ```csharp
   public class NewFrameworkAdapter : IFramework
   {
       public string Name => "NewFramework";
       public string CreateWrappedMessage(IMessageRequest request);
   }
   ```
2. Register in `StaticFrameworkProvider`

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
| Framework adapters | `Iris.Brokers/Frameworks/` |
| Shared UI components | `Iris.Components/Shared/` |
| Feature components | `Iris.Components/{Feature}/` |
| Local DB context | `Iris.Desktop/Infrastructure/IrisLiteDbContext.cs` |
| Theme/styling | `Iris.Components/Theme/` |

## Known Patterns to Preserve

1. **Auto-discovery of local connections** - `AutoDiscovery.cs` tries to connect to local RabbitMQ and Azure Storage Emulator on startup
2. **Framework message wrapping** - Messages can be wrapped in MassTransit/NServiceBus envelopes via `IFramework`
3. **Internal message bus** - Components communicate via `IMessageBus` pub/sub, not direct references
4. **Dynamic type generation** - `CodeGenerator` uses `System.Reflection.Emit` to create runtime types from loaded assemblies

## Areas Needing Future Work

1. **Connection Persistence** - Connections are currently in-memory only; saving to LiteDB would improve UX
2. **Retry/Resilience** - `Microsoft.Extensions.Resilience` is available but not wired up for broker connections
3. **Cancellation Tokens** - Many async methods don't accept `CancellationToken`
4. **OpenTelemetry** - Packages are referenced but tracing isn't implemented

## Debugging Tips

1. **Broker connection issues** - Check `AutoDiscovery.cs` for local connection logic
2. **Message not sending** - Verify `LocalConnectionManager.SendMessageAsync()` flow
3. **UI not updating** - Check if `StateHasChanged()` is being called, or if using the internal `IMessageBus`
4. **Template/History issues** - Check `IrisLiteDbContext` and repository implementations

---

*This document was created to help AI agents quickly understand and contribute to the Iris codebase. Update it as the project evolves.*

