using System.Text.Json.Nodes;
using Iris.Contracts.Assemblies.Models;

namespace Iris.Contracts.Assemblies;

public interface ISampleJsonGenerator
{
    JsonNode GenerateSample(TypeData typeData);
}
