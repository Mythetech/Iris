using System.Reflection;
using System.Runtime.Loader;

namespace Iris.Assemblies;

public class LoadedAssembly
{
    public required Assembly Assembly { get; init; }
    public required AssemblyLoadContext Context { get; init; }
}
