using System;
using Iris.Contracts.Brokers.Models;

namespace Iris.Contracts.Brokers.Endpoints
{
    public static class GetSupportedProviders
    {
        public record GetSupportedProvidersRequest();

        public record GetSupportedProvidersResponse(List<SupportedProvider> Providers);
    }
}

