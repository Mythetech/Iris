using System.Reflection;

namespace Iris.Assemblies;

public interface IAssemblyLoadService
{
    public Task<Assembly?> LoadAssemblyAsync(Stream assembly);
}