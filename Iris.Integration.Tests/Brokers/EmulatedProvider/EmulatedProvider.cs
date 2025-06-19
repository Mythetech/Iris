using System;
using Iris.Brokers;
using Iris.Brokers.Models;

namespace Iris.Integration.Tests.Brokers.EmulatedProvider
{
    public class EmulatedProvider : IConnector
    {
        public EmulatedProvider()
        {
        }

        public string Provider => "Mock";

        private int _createdConnections = 0;

        public Task<IConnection?> ConnectAsync(ConnectionData data, bool discoverEndpoints = true)
        {
            if (data == null || data.ConnectionString == null && data.Uri == null)
                return Task.FromResult(default(IConnection?));

            IConnection connection = new MockConnection($"fakeaddress-{data.ConnectionString}", 1);

            return Task.FromResult(connection);
        }

        private class MockConnection : IConnection
        {
            public MockConnection(string address, int endpointCount = 0)
            {
                Address = address;
                EndpointCount = endpointCount;
            }

            private IConnector? _override;

            public IConnector Connector { get => _override ?? new EmulatedProvider(); set => _override = value; }

            public string Name => "Mock";

            public string Address { get; }

            public int EndpointCount { get; }

            public Task<List<EndpointDetails>> GetEndpointsAsync()
            {
                var endpoints = new List<EndpointDetails>();
                for (int i = 0; i < EndpointCount; i++)
                {
                    endpoints.Add(new EndpointDetails()
                    {
                        Address = Address,
                        Name = "Endpoint - " + i + 1,
                        Provider = "Mock",
                        Type = "Queue"
                    });
                }

                return Task.FromResult(endpoints);
            }

            public Task ReadAsync(string messageId)
            {
                return Task.CompletedTask;
            }

            public Task SendAsync(EndpointDetails endpoint, string json)
            {
                return Task.CompletedTask;
            }
        }
    }
}


