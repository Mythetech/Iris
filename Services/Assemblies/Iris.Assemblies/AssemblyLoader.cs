using System.Runtime.Loader;
using Microsoft.Extensions.Logging;

namespace Iris.Assemblies;

public class AssemblyLoader : IAssemblyLoadService
{
    private readonly ILogger<AssemblyLoader> _logger;

    public AssemblyLoader(ILogger<AssemblyLoader> logger)
    {
        _logger = logger;
    }

    public Task<LoadedAssembly?> LoadAssemblyAsync(Stream assembly)
    {
        var context = new AssemblyLoadContext($"Iris-{Guid.NewGuid():N}", isCollectible: true);

        try
        {
            var asm = context.LoadFromStream(assembly);
            return Task.FromResult<LoadedAssembly?>(new LoadedAssembly
            {
                Assembly = asm,
                Context = context
            });
        }
        catch (Exception e)
        {
            _logger.LogError("Unable to load assembly from stream: {Message}", e.Message);
            context.Unload();
            return Task.FromResult<LoadedAssembly?>(null);
        }
    }
}
