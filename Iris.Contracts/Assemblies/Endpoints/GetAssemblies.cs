using Iris.Contracts.Assemblies.Models;

namespace Iris.Contracts.Assemblies.Endpoints
{
    public static class GetAssemblies
    {
        public record GetAssembliesResponse(List<AssemblyData> AssemblyData);
    }
}

