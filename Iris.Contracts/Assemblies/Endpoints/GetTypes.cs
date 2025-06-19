using Iris.Contracts.Assemblies.Models;

namespace Iris.Contracts.Types.Endpoints
{
    public static class GetTypes
    {
        public record GetTypesResponse(List<TypeData> TypeData);
    }
}

