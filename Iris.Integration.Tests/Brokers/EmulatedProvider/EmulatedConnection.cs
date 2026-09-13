using System;
using Iris.Brokers;
using Iris.Brokers.Models;

namespace Iris.Integration.Tests.Brokers.EmulatedProvider
{

    public class EmulatedConnection : IConnection, IHeaderCarrier, ITransportPropertyCarrier
    {
        public EmulatedConnection() { }

        public EmulatedConnection(int endpointCount = 0, string address = "internal")
        {
            _endpointCount = endpointCount;

            if (endpointCount > 0)
            {
                for (int i = 0; i < endpointCount; i++)
                {
                    _details ??= new();

                    _details.Add(new()
                    {
                        Address = $"{Address}/{i}",
                        Name = $"Endpoint-{i}",
                        Provider = Connector.Provider,
                        Type = "Queue"
                    });
                }
            }

            _address = address;
        }

        private List<EndpointDetails>? _details;

        private readonly string _address = "";

        private readonly IConnector _mockProvider = new EmulatedProvider();

        private IConnector? _override;
        private int _endpointCount;

        public IConnector Connector { get => _mockProvider; set => _override = value; }

        public Guid Id { get; } = Guid.NewGuid();

        public string Name => nameof(EmulatedConnection);

        public int EndpointCount => _endpointCount;

        public string Address => _address;

        public Task<List<EndpointDetails>> GetEndpointsAsync()
        {
            if (_details?.Count > 0)
                return Task.FromResult(_details);

            return Task.FromResult(new List<EndpointDetails>());
        }

        public MessageRequest? LastRequest { get; private set; }

        public int MaxHeaderCount => int.MaxValue;

        public bool IsValidHeaderKey(string key) => !string.IsNullOrWhiteSpace(key);

        public IReadOnlySet<HeaderDataType> SupportedDataTypes { get; } =
            new HashSet<HeaderDataType>(Enum.GetValues<HeaderDataType>());

        public IReadOnlySet<TransportProperty> SupportedProperties { get; } =
            new HashSet<TransportProperty>(Enum.GetValues<TransportProperty>());

        public Task SendAsync(EndpointDetails endpoint, MessageRequest message)
        {
            LastRequest = message;
            return Task.CompletedTask;
        }

        public Task ReadAsync(string messageId)
        {
            return Task.CompletedTask;
        }
    }

}

