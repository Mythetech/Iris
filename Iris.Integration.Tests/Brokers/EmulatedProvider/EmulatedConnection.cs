using System;
using Iris.Brokers;

namespace Iris.Integration.Tests.Brokers.EmulatedProvider
{

    public class EmulatedConnection : IConnection
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

        public string Name => nameof(EmulatedConnection);

        public int EndpointCount => _endpointCount;

        public string Address => _address;

        public Task<List<EndpointDetails>> GetEndpointsAsync()
        {
            if (_details?.Count > 0)
                return Task.FromResult(_details);

            return Task.FromResult(new List<EndpointDetails>());
        }

        public Task SendAsync(EndpointDetails endpoint, string json)
        {
            return Task.CompletedTask;
        }

        public Task ReadAsync(string messageId)
        {
            return Task.CompletedTask;
        }
    }

}

