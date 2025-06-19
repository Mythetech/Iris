using System;
using Iris.Contracts.Brokers.Models;

namespace Iris.Contracts.Brokers.Endpoints
{
    public static class GetAllProviders
    {
        public record GetAllProvidersResponse(List<Provider> Providers);
    }
}

