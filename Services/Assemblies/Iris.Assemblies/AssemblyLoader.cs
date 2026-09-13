using Microsoft.Extensions.Logging;

namespace Iris.Assemblies;

public class AssemblyLoader : IAssemblyLoadService
{
    private readonly ILogger<AssemblyLoader> _logger;

    public AssemblyLoader(ILogger<AssemblyLoader> logger)
    {
        _logger = logger;
    }

    public Task<LoadedAssembly?> LoadAssemblyAsync(Stream assembly, string? assemblyPath = null)
    {
        var context = new IrisAssemblyLoadContext($"Iris-{Guid.NewGuid():N}", assemblyPath);

        try
        {
            // Loaded from the stream rather than the path so the file is never locked and the
            // same assembly can be replaced on disk and loaded again without restarting Iris.
            var asm = context.LoadFromStream(assembly);
            return Task.FromResult<LoadedAssembly?>(new LoadedAssembly
            {
                Assembly = asm,
                Context = context
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to load assembly from stream");
            context.Unload();
            return Task.FromResult<LoadedAssembly?>(null);
        }
    }
}
