using Iris.Contracts.Assemblies.Models;

namespace Iris.Components.Messaging;

public sealed record PickedType(TypeData Type, string AssemblyName);
