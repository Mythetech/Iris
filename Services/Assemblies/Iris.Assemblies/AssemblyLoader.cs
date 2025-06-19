using System.Reflection;
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

    private AssemblyLoadContext GetDefaultAssemblyContext()
    {
        return new AssemblyLoadContext("Iris");
    }
    
    public Task<Assembly?> LoadAssemblyAsync(Stream assembly)
    {
        var context = GetDefaultAssemblyContext();

        Assembly? asm = default;
        
        try
        {
            asm = context.LoadFromStream(assembly);
        }
        catch (Exception e)
        {
            _logger.LogError("Unable to load assembly from stream: {Message}", e.Message);
        }

        return Task.FromResult(asm);
    }
}