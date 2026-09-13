using System.Text.Json;
using Iris.Assemblies;
using Iris.Brokers;
using Iris.Brokers.Exceptions;
using Iris.Brokers.Frameworks;
using Iris.Brokers.Models;
using Iris.Components.Brokers;
using Mythetech.Framework.Infrastructure.MessageBus;
using Iris.Components.Messaging;
using Iris.Components.PackageManagement;
using Iris.Contracts.Assemblies;
using Iris.Contracts.Brokers.Endpoints;
using Iris.Contracts.Brokers.Models;
using Iris.Contracts.Brokers.Events;
using Iris.Contracts.Messaging.Events;
using Iris.Contracts.Messaging.Frameworks;
using Iris.Contracts.Messaging.Models;
using Iris.Contracts.Results;
using Iris.Desktop.PackageManagement;
using Microsoft.Extensions.Logging;
using ConnectionData = Iris.Contracts.Brokers.Models.ConnectionData;
using EndpointDetails = Iris.Contracts.Brokers.Models.EndpointDetails;
using CreateConnectionResponse = Iris.Contracts.Brokers.Endpoints.CreateConnection.CreateConnectionResponse;
using DeleteConnectionResponse = Iris.Contracts.Brokers.Endpoints.DeleteConnection.DeleteConnectionResponse;
using Provider = Iris.Contracts.Brokers.Models.Provider;


namespace Iris.Desktop.Brokers;

public class LocalConnectionManager : IBrokerService, IMessageService
{
    private readonly IBrokerConnectionManager _connectionManager;
    private readonly IMessageBus _bus;
    private readonly IFrameworkProvider _frameworks;
    private readonly ISampleJsonGenerator _sampleJson;
    private readonly IPackageService _packageService;
    private readonly AssemblySettings _assemblySettings;
    private readonly MessageState _state;
    private readonly ConnectionRepository _connectionRepository;
    private readonly ILogger<LocalConnectionManager> _logger;

    public LocalConnectionManager(IBrokerConnectionManager connectionManager, IMessageBus bus,
        IFrameworkProvider frameworks, ISampleJsonGenerator sampleJson, IPackageService packageService,
        AssemblySettings assemblySettings, MessageState state,
        ConnectionRepository connectionRepository, ILogger<LocalConnectionManager> logger)
    {
        _connectionManager = connectionManager;
        _bus = bus;
        _frameworks = frameworks;
        _sampleJson = sampleJson;
        _packageService = packageService;
        _assemblySettings = assemblySettings;
        _state = state;
        _connectionRepository = connectionRepository;
        _logger = logger;
    }

    public async Task<Result<CreateConnectionResponse>> CreateConnectionAsync(ConnectionData data)
    {
        var provider = _connectionManager.GetProviders()
            .FirstOrDefault(x => x.Provider.Equals(data.Provider, StringComparison.OrdinalIgnoreCase));

        if (provider == null)
            return new Failure<CreateConnectionResponse>("Provider not found");

        IConnection? connection = default;
        try
        {
            connection = await provider.ConnectAsync(Iris.Brokers.Models.ConnectionData.FromContract(data));
        }
        catch (InvalidConnectionException ex)
        {
            return new Failure<CreateConnectionResponse>(ex.Message);
        }

        if (connection == null)
            return new Failure<CreateConnectionResponse>("Connection error");

        await _connectionManager.AddConnectionAsync(connection);
        await _bus.PublishAsync(new ConnectionCreated(connection.Connector.Provider, connection.Address));

        _connectionRepository.Save(SavedConnection.FromConnectionData(data.Provider, connection.Address, data));

        var endpoints = new List<EndpointDetails>();

        endpoints = (await connection.GetEndpointsAsync()).Select(x => new EndpointDetails()
        {
            Name = x.Name,
            Address = x.Address,
            Provider = x.Provider,
            Type = x.Type,
        }).ToList();

        return new Success<CreateConnectionResponse>(new CreateConnectionResponse(true, connection.Address, endpoints));
    }

    public async Task<Result<DeleteConnectionResponse>> DeleteConnectionAsync(string address)
    {
        bool success = await _connectionManager.RemoveConnectionAsync(address);

        if (success)
        {
            _connectionRepository.Delete(address);
            await _bus.PublishAsync(new ConnectionDeleted(address, address));
            return new Success<DeleteConnectionResponse>(new(success));
        }

        return new Failure<DeleteConnectionResponse>("Connection not found");
    }

    public async Task<List<EndpointDetails>> GetEndpointsAsync()
    {
        var endpoints = await _connectionManager.GetEndpointsAsync();

        return endpoints.Select(x => new EndpointDetails()
            {
                Name = x.Name,
                Address = x.Address,
                Provider = x.Provider,
                Type = x.Type,
            })
            .ToList();
    }

    public async Task<List<Provider>> GetProvidersAsync()
    {
        var connections = await _connectionManager.GetConnectionsAsync();

        return connections.Select(p => new Provider()
        {
            Id = p.Id,
            Name = p.Connector.Provider,
            Address = p.Address,
            Endpoints = p.EndpointCount,
            Transport = p.Name
        }).ToList();
    }

    public async Task<Provider?> GetConnectionByIdAsync(Guid id)
    {
        var connections = await _connectionManager.GetConnectionsAsync();
        var match = connections.FirstOrDefault(c => c.Id == id);
        if (match is null)
            return null;

        return new Provider
        {
            Id = match.Id,
            Name = match.Connector.Provider,
            Address = match.Address,
            Endpoints = match.EndpointCount,
            Transport = match.Name,
        };
    }

    public Task<List<SupportedProvider>> GetSupportedProvidersAsync()
    {
        var providers = _connectionManager.GetProviders();

        return Task.FromResult(providers.Select(x => new SupportedProvider()
            {
                Name = x.Provider,
            })
            .ToList());
    }

    public async Task RefreshDataAsync()
    {
        await _connectionManager.GetConnectionsAsync();

    }

    /// <summary>
    /// Builds the sample body for a message type the same way the type picker does, through
    /// <see cref="TypeMapper"/> and <see cref="ISampleJsonGenerator"/>.
    ///
    /// This used to emit a dynamic clone of the type and serialize a default instance of it.
    /// That clone was defined with <c>AssemblyBuilderAccess.Run</c>, which is non-collectible,
    /// while every user type comes from the collectible context <see cref="AssemblyLoader"/>
    /// creates, so the moment a message had a property of a user-defined type, which is any
    /// nested class, enum or record, defining the field threw and the caller showed an empty
    /// editor. The types users most want a sample for were exactly the ones that never worked.
    /// </summary>
    public Task<string> GetMessageStructureAsync(string messageType)
    {
        var type = ResolveLoadedType(messageType);

        if (type is null)
            return Task.FromResult("");

        try
        {
            var sample = _sampleJson.GenerateSample(type.ToContract(_assemblySettings.MaxTypeDepth));

            return Task.FromResult(sample.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e)
        {
            // Reflecting over a loaded type can still fail when a dependency it needs is not
            // resolvable. An empty editor is the right fallback, but it must not be silent.
            _logger.LogError(e, "Unable to build a sample body for {MessageType}", messageType);
            return Task.FromResult("");
        }
    }

    private string? ResolveMessageAssemblyName(string messageType)
        => ResolveLoadedType(messageType)?.Assembly.GetName().Name;

    /// <summary>
    /// Endpoint names arrive spelled the way their broker spells them, so a RabbitMQ exchange
    /// carries <c>Namespace:Type</c> where the .NET name has a dot. Matching on the short name
    /// first keeps a bare queue name like <c>OrderPlaced</c> working.
    /// </summary>
    private Type? ResolveLoadedType(string messageType)
    {
        if (string.IsNullOrWhiteSpace(messageType))
            return null;

        if (_packageService is not LocalPackageService local)
            return null;

        var normalized = messageType.Replace(":", ".");
        var types = local.GetLoadedTypes().Distinct().ToList();

        return types.FirstOrDefault(x => x.Name == normalized)
               ?? types.FirstOrDefault(x => x.FullName != null && x.FullName.Equals(normalized));
    }

    public async Task<Result<bool>> SendMessageAsync(string messageType, string messageJson, string? address,
        string? framework = default,
        Dictionary<string, string>? properties = default, Dictionary<string, string>? headers = default)
    {
        var message = new Message()
        {
            MessageType = messageType,
            Address = address,
            Data = messageJson,
            Framework = framework,
            Headers = headers,
            Properties = properties,
            GenerateIrisHeaders = _state.SendIrisHeader
        };

        return await SendMessageAsync(message);
    }

    public async Task<Result<bool>> SendMessageAsync(Message message)
    {
        var connection = await _connectionManager.GetConnectionAsync(message.Address);
        if (connection is null)
            return new Failure<bool>("Connection not found.");

        var properties = message.Properties ?? new Dictionary<string, string>();

        var typeName = properties.TryGetValue(FrameworkInputs.TypeName, out var explicitType) && !string.IsNullOrWhiteSpace(explicitType)
            ? explicitType
            : message.MessageType;

        var assemblyName = properties.TryGetValue(FrameworkInputs.AssemblyName, out var explicitAssembly) && !string.IsNullOrWhiteSpace(explicitAssembly)
            ? explicitAssembly
            : ResolveMessageAssemblyName(typeName);

        var request = MessageRequest.Create(message.MessageType,
            message.Data ?? string.Empty,
            _state.SendIrisHeader,
            typeName,
            message.Framework,
            message.Headers,
            properties,
            assemblyName);

        if (!string.IsNullOrWhiteSpace(request.Framework))
        {
            var framework = _frameworks.GetFramework(request.Framework);
            if (framework is null)
                return new Failure<bool>($"Unknown framework '{request.Framework}'.");

            var compatibility = FrameworkCompatibility.Check(framework, connection, request.Headers.Keys);
            if (!compatibility.Supported)
                return new Failure<bool>(compatibility.Reason!);

            try
            {
                request.WrapMessage(framework);
            }
            catch (ArgumentException ex)
            {
                return new Failure<bool>(ex.Message);
            }

            FrameworkCompatibility.RemoveDropped(request, compatibility);
        }

        request.Properties.TryGetValue("EndpointType", out string? endpointType);

        if (!string.IsNullOrWhiteSpace(endpointType))
        {
            request.Properties.Remove("EndpointType");
        }

        try
        {
            await connection.SendAsync(new Iris.Brokers.EndpointDetails()
                {
                    Address = message.Address,
                    Name = message.MessageType,
                    Provider = connection.Connector.Provider,
                    Type = endpointType ?? "Queue"
                },
                request);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new Failure<bool>($"send failed: {ex.Message}");
        }

        var evt = new MessageSent
        {
            Address = message.Address,
            Provider = connection.Connector.Provider,
            Message = request.Json,
            Endpoint = message.MessageType,
            Headers = request.Headers,
            Properties = request.Properties,
        };

        await _bus.PublishAsync(evt);

        return Success<bool>.Create();
    }

    public Task<Result<IReadOnlyList<ReceivedMessageDto>>> PeekMessagesAsync(
        string address, string endpointName, int count, CancellationToken cancellationToken = default)
        => ReadAsync<IMessagePeeker>(
            address, endpointName, count, cancellationToken,
            "peek",
            (peeker, ep, c, ct) => peeker.PeekAsync(ep, Math.Min(c, peeker.MaxPeekBatchSize), ct));

    public Task<Result<IReadOnlyList<ReceivedMessageDto>>> ReceiveMessagesAsync(
        string address, string endpointName, int count, CancellationToken cancellationToken = default)
        => ReadAsync<IMessageReceiver>(
            address, endpointName, count, cancellationToken,
            "receive",
            (receiver, ep, c, ct) => receiver.ReceiveAsync(ep, Math.Min(c, receiver.MaxReceiveBatchSize), ct));

    public Task<Result<IReadOnlyList<ReceivedMessageDto>>> PeekDeadLetterMessagesAsync(
        string address, string endpointName, int count, CancellationToken cancellationToken = default)
        => ReadAsync<IDeadLetterPeeker>(
            address, endpointName, count, cancellationToken,
            "dead-letter peek",
            (peeker, ep, c, ct) => peeker.PeekDeadLetterAsync(ep, c, ct));

    public Task<Result<IReadOnlyList<ReceivedMessageDto>>> ReceiveDeadLetterMessagesAsync(
        string address, string endpointName, int count, CancellationToken cancellationToken = default)
        => ReadAsync<IDeadLetterReceiver>(
            address, endpointName, count, cancellationToken,
            "dead-letter receive",
            (receiver, ep, c, ct) => receiver.ReceiveDeadLetterAsync(ep, c, ct));

    private async Task<Result<IReadOnlyList<ReceivedMessageDto>>> ReadAsync<TReader>(
        string address,
        string endpointName,
        int count,
        CancellationToken cancellationToken,
        string operationName,
        Func<TReader, Iris.Brokers.EndpointDetails, int, CancellationToken, Task<IReadOnlyList<Iris.Brokers.Models.ReceivedMessage>>> read)
        where TReader : class
    {
        var connection = await _connectionManager.GetConnectionAsync(address);
        if (connection is null)
            return new Failure<IReadOnlyList<ReceivedMessageDto>>("Connection not found.");
        if (connection is not TReader reader)
            return new Failure<IReadOnlyList<ReceivedMessageDto>>(
                $"{connection.Connector.Provider} does not support {operationName}.");

        var endpoint = new Iris.Brokers.EndpointDetails
        {
            Address = address,
            Name = endpointName,
            Provider = connection.Connector.Provider,
            Type = "Queue",
        };

        try
        {
            var messages = await read(reader, endpoint, count, cancellationToken);
            return new Success<IReadOnlyList<ReceivedMessageDto>>(messages.Select(MapMessage).ToList());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new Failure<IReadOnlyList<ReceivedMessageDto>>($"{operationName} failed: {ex.Message}");
        }
    }

    private static ReceivedMessageDto MapMessage(Iris.Brokers.Models.ReceivedMessage msg)
    {
        var dto = new ReceivedMessageDto
        {
            Body = msg.Body,
            MessageId = msg.MessageId,
            CorrelationId = msg.CorrelationId,
            ContentType = msg.ContentType,
            Headers = new Dictionary<string, string>(msg.Headers),
            Properties = new Dictionary<string, string>(msg.Properties),
            DeliveryCount = msg.DeliveryCount,
            EnqueuedTimeUtc = msg.EnqueuedTimeUtc,
            ExpiresAtUtc = msg.ExpiresAtUtc,
            SizeInBytes = msg.SizeInBytes,
            Source = (Iris.Contracts.Messaging.Models.ReadSource)msg.Source,
            Provider = msg.Provider,
        };

        if (msg.Native is not null)
        {
            if (msg.Native.LockToken is not null) dto.Native["LockToken"] = msg.Native.LockToken;
            if (msg.Native.ReceiptHandle is not null) dto.Native["ReceiptHandle"] = msg.Native.ReceiptHandle;
            if (msg.Native.PopReceipt is not null) dto.Native["PopReceipt"] = msg.Native.PopReceipt;
            if (msg.Native.RoutingKey is not null) dto.Native["RoutingKey"] = msg.Native.RoutingKey;
            if (msg.Native.Exchange is not null) dto.Native["Exchange"] = msg.Native.Exchange;
            foreach (var kvp in msg.Native.Extra)
                dto.Native[kvp.Key] = kvp.Value;
        }

        return dto;
    }

    public Task<Iris.Contracts.Brokers.Models.ReaderCapabilitiesDto?> GetReaderCapabilitiesAsync(string address)
    {
        return GetReaderCapabilitiesInternalAsync(address);
    }

    public async Task<Iris.Contracts.Brokers.Models.EndpointPropertiesDto?> GetEndpointPropertiesAsync(
        string address,
        string endpointName,
        string? type,
        CancellationToken cancellationToken = default)
    {
        var connection = await _connectionManager.GetConnectionAsync(address);
        if (connection is not IEndpointInspector inspector)
            return null;

        try
        {
            return await inspector.InspectAsync(endpointName, type, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private async Task<Iris.Contracts.Brokers.Models.ReaderCapabilitiesDto?> GetReaderCapabilitiesInternalAsync(string address)
    {
        var connection = await _connectionManager.GetConnectionAsync(address);
        if (connection is null)
            return null;

        return new Iris.Contracts.Brokers.Models.ReaderCapabilitiesDto(
            CanPeek: connection is IMessagePeeker,
            CanReceive: connection is IMessageReceiver,
            CanPeekDeadLetter: connection is IDeadLetterPeeker,
            CanReceiveDeadLetter: connection is IDeadLetterReceiver,
            MaxPeekBatchSize: (connection as IMessagePeeker)?.MaxPeekBatchSize ?? 0,
            MaxReceiveBatchSize: (connection as IMessageReceiver)?.MaxReceiveBatchSize ?? 0);
    }
}