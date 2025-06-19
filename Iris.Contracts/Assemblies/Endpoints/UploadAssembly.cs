
using Iris.Contracts.Assemblies.Models;

namespace Iris.Contracts.Assemblies.Endpoints
{
    public static class UploadAssembly
    {

        public record UploadAssemblyResponse(List<string> DiscoveredTypes);
    }
}

