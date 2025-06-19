using System.Reflection;
using Iris.Assemblies;
using Iris.Components.PackageManagement;
using Iris.Contracts.Assemblies.Models;
using Iris.Contracts.Brokers.Models;
using Iris.Contracts.Results;
using Microsoft.AspNetCore.Components.Forms;

namespace Iris.Desktop.PackageManagement;

public class LocalPackageService : IPackageService
{
    private readonly IAssemblyLoadService _assemblyLoader;
    private List<Assembly> _assemblies;
    
    public LocalPackageService(IAssemblyLoadService assemblyLoader)
    {
        _assemblyLoader = assemblyLoader;
    }

    public List<Type> GetLoadedTypes()
    {
        _assemblies ??= [];
        
        return _assemblies.SelectMany(assembly => assembly.GetTypes()).ToList();
    }

    public Task<List<AssemblyData>> GetLoadedAssembliesAsync()
    {
        _assemblies ??= [];
        
        return Task.FromResult(_assemblies.Select(x => new AssemblyData()
        {
            Name = x.GetName().Name ?? "--",
            FullyQualifiedName = x.FullName ?? "--",
            ExportedTypeNames = x.ExportedTypes.Select(x => x.Name).ToList(),
            Version = x.GetName().Version?.ToString() ?? "--",
            ExportedTypes = x.ExportedTypes.Select(y => y.ToContract()).ToList()
        }).ToList());
    }

    public async Task<Result<AssemblyData>> UploadAssemblyAsync(IBrowserFile file)
    {

        using var stream = file.OpenReadStream();
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        memoryStream.Position = 0; // Reset to the beginning of the stream

        var asm = await _assemblyLoader.LoadAssemblyAsync(memoryStream);

        if (asm == null)
        {
            return new Failure<AssemblyData>("Failed to load assembly. The file may be invalid or unsupported.");
        }
        
        if(!_assemblies.Any(x => x.FullName == asm.FullName))
            _assemblies.Add(asm);

        return new Success<AssemblyData>(asm.ToContract());
    }
}