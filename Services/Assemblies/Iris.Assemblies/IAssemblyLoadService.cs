namespace Iris.Assemblies;

public interface IAssemblyLoadService
{
    /// <param name="assemblyPath">
    /// The file the stream was read from, when there is one. It is what lets the load context
    /// find the assembly's dependencies, so a caller that has a path should always pass it.
    /// </param>
    public Task<LoadedAssembly?> LoadAssemblyAsync(Stream assembly, string? assemblyPath = null);
}
