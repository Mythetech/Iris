namespace Iris.Assemblies;

public interface IAssemblyLoadService
{
    public Task<LoadedAssembly?> LoadAssemblyAsync(Stream assembly);
}
