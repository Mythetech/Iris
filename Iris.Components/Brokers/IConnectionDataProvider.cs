using System;
using Iris.Contracts.Brokers.Models;

namespace Iris.Components.Brokers
{
    public interface IConnectionDataProvider
    {
        public string ProviderName { get; }

        public ConnectionData Payload { get; }
    }
}

