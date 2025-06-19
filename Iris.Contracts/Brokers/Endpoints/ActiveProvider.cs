using Iris.Contracts.Brokers.Models;

namespace Iris.Contracts.Brokers.Endpoints
{
    public static class ActiveProvider
    {
        public record ActiveProviderRequest(bool SetFirstIfNotFound = true);

        public record ActiveProviderResponse(Provider? Provider);
    }
}

