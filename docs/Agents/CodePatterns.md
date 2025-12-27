# Code Patterns and Conventions

This document details the specific code patterns used throughout Iris to maintain consistency.

## Result Pattern

Operations that can fail return `Result<T>` instead of throwing exceptions:

```csharp
// Definition
public class Result<T>
{
    public bool Error { get; set; }
    public string Message { get; set; }
    public T Value { get; set; }
}

// Success
return new Success<bool>(true, "Operation completed");

// Failure
return new Failure<CreateConnectionResponse>("Provider not found");

// Handling in UI
HandleResult(result, 
    success: x => "Connection created",
    failure: x => result.Message);
```

## Service Registration Pattern

Services are registered via generic type parameters for flexibility between Desktop and Cloud deployments:

```csharp
services.AddIrisComponentServices<
    TBrokerService,      // IBrokerService implementation
    TMessageService,     // IMessageService implementation  
    TTemplateService,    // ITemplateService implementation
    TPackageService,     // IPackageService implementation
    THistoryService,     // IHistoryService implementation
    TAdminService,       // IAdminService implementation
    TMessageLayoutService // IMessagingLayoutService implementation
>();
```

Desktop uses local implementations; Cloud would use HTTP clients.

## Component Base Classes

### IrisBaseComponent
For reusable components with common HTML attributes:

```csharp
public class IrisBaseComponent : ComponentBase
{
    [Parameter] public string Class { get; set; } = "";
    [Parameter] public string Style { get; set; } = "";
    [Parameter] public string? Id { get; set; }
    [Parameter(CaptureUnmatchedValues = true)]
    public virtual IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }
}
```

### IrisPageBase
For full pages with authorization and common functionality:

```csharp
[Authorize]
public class IrisPageBase : LayoutComponentBase
{
    [Inject] public NavigationManager NavigationManager { get; set; }
    [Inject] public ISnackbar Snackbar { get; set; }
    
    public void HandleResult<T>(Result<T> result, string successMessage, string error);
    public void NotifyError(string message, string title = "Error");
}
```

## Internal Message Bus Pattern

Components communicate via pub/sub to stay decoupled:

```csharp
// Define a message
public record SetText(string Value);

// Consumer implementation
public class MyConsumer : IConsumer<SetText>
{
    public Task Consume(SetText message)
    {
        // Handle message
        return Task.CompletedTask;
    }
}

// Publishing
await _messageBus.PublishAsync(new SetText("Hello"));

// Registration (automatic via assembly scanning)
services.AddIrisMessageBus(typeof(Program).Assembly);
services.UseIrisMessageBus(typeof(Program).Assembly);
```

## Broker Connector Pattern

Each broker has a Connector (factory) and Connection (active connection):

```csharp
// Connector creates connections
public interface IConnector
{
    string Provider { get; }
    Task<IConnection?> ConnectAsync(ConnectionData data, bool discoverEndpoints = true);
}

// Connection manages the active session
public interface IConnection
{
    IConnector Connector { get; set; }
    string Name { get; }
    string Address { get; }
    int EndpointCount { get; }
    Task<List<EndpointDetails>> GetEndpointsAsync();
    Task SendAsync(EndpointDetails endpoint, string json);
}
```

## Framework Adapter Pattern

Message frameworks (MassTransit, NServiceBus) wrap messages in envelopes:

```csharp
public interface IFramework
{
    string Name { get; }
    string CreateWrappedMessage(IMessageRequest request);
}

// Usage
if (!string.IsNullOrWhiteSpace(request.Framework))
    request.WrapMessage(_frameworks);
```

## Repository Pattern

Local data uses LiteDB with typed repositories:

```csharp
public class HistoryRepository : IRepository
{
    private readonly IrisLiteDbContext _dbContext;
    public string DbKey { get; } = "history";

    public ILiteCollection<PersistentHistoryRecord> GetHistory()
    {
        return _dbContext.GetCollection<PersistentHistoryRecord>(DbKey);
    }
}
```

## State Management Pattern

Feature state is managed via scoped services:

```csharp
// State class
public class MessageState
{
    public string? SelectedFramework { get; set; }
    public int Delay { get; set; }
    public int Repeat { get; set; }
    public Dictionary<string, string> Headers { get; set; }
    
    public event Action? OnStateChanged;
    public void NotifyStateChanged() => OnStateChanged?.Invoke();
}

// Inject and use in components
[Inject] public MessageState MessageState { get; set; }
```

## Dynamic Type Generation

For creating runtime types from loaded assemblies:

```csharp
public class CodeGenerator : ICodeGenerator
{
    public Type Create(Type type)
    {
        var typeBuilder = CreateTypeBuilder(type.Name + "Dynamic");
        
        foreach (var property in type.GetProperties())
        {
            CreateProperty(typeBuilder, property.Name, property.PropertyType);
        }
        
        return typeBuilder.CreateType();
    }
}
```

## Testing Patterns

### Component Test Setup

```csharp
public class IrisTestContext : TestContext
{
    public IrisTestContext()
    {
        Services.AddMudServices();
        JSInterop.SetupVoid("mudPopover.initialize", _ => true);
        // ... other MudBlazor interop setups
    }
}
```

### Mocking with NSubstitute

```csharp
public class MyTests : IrisTestContext
{
    private readonly ISnackbar _snackbar;
    
    public MyTests()
    {
        _snackbar = Substitute.For<ISnackbar>();
        Services.RemoveAll(typeof(ISnackbar));
        Services.AddSingleton(_snackbar);
    }
    
    [Fact]
    public void Test_Shows_Notification()
    {
        // ... test logic
        _snackbar.Received().Add<IrisSnackbar>(Arg.Any<Dictionary<string, object>>(), Severity.Success);
    }
}
```

### Integration Tests with Containers

```csharp
public class RabbitMqTests : IAsyncLifetime
{
    private readonly RabbitMqContainer _container = new RabbitMqBuilder()
        .WithImage("rabbitmq:3-management")
        .WithUsername("guest")
        .WithPassword("guest")
        .Build();

    public Task InitializeAsync() => _container.StartAsync();
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task Can_Connect()
    {
        var connectionData = new ConnectionData
        {
            ConnectionString = _container.GetConnectionString(),
            Username = "guest",
            Password = "guest"
        };
        // ... test
    }
}
```

## Blazor Component Patterns

### Two-Way Binding

```csharp
[Parameter] public string Value { get; set; }
[Parameter] public EventCallback<string> ValueChanged { get; set; }

private async Task UpdateValue(string newValue)
{
    Value = newValue;
    await ValueChanged.InvokeAsync(newValue);
}
```

### Query Parameters

```csharp
[SupplyParameterFromQuery]
public string? Provider { get; set; }

[SupplyParameterFromQuery]
public string? Endpoint { get; set; }
```

### Lifecycle with Disposal

```csharp
@implements IDisposable

@code {
    protected override void OnInitialized()
    {
        SomeState.OnChange += HandleChange;
    }
    
    public void Dispose()
    {
        SomeState.OnChange -= HandleChange;
    }
}
```

