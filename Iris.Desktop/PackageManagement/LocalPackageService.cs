using System.Reflection;
using Iris.Assemblies;
using Iris.Assemblies.Messages;
using Iris.Components.PackageManagement;
using Iris.Contracts.Assemblies.Models;
using Iris.Contracts.Results;
using Microsoft.AspNetCore.Components.Forms;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Iris.Desktop.PackageManagement;

public class LocalPackageService : IPackageService, IDisposable
{
    private readonly IAssemblyLoadService _assemblyLoader;
    private readonly AssemblySettings _settings;
    private readonly PackageRepository _packageRepository;
    private readonly IMessageBus _bus;
    private List<LoadedAssembly> _assemblies = [];

    public LocalPackageService(IAssemblyLoadService assemblyLoader, AssemblySettings settings, PackageRepository packageRepository, IMessageBus bus)
    {
        _assemblyLoader = assemblyLoader;
        _settings = settings;
        _packageRepository = packageRepository;
        _bus = bus;
    }

    public List<Type> GetLoadedTypes()
    {
        return _assemblies.SelectMany(la => la.Assembly.GetTypes()).ToList();
    }

    public Task<List<AssemblyData>> GetLoadedAssembliesAsync()
    {
        return Task.FromResult(_assemblies.Select(la =>
            la.Assembly.ToContract(_settings.MaxTypeDepth)).ToList());
    }

    public async Task<Result<AssemblyData>> UploadAssemblyAsync(IBrowserFile file)
    {
        using var stream = file.OpenReadStream();
        return await LoadAssemblyFromStreamAsync(stream, assemblyPath: null);
    }

    public async Task<Result<AssemblyData>> UploadAssemblyAsync(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        var result = await LoadAssemblyFromStreamAsync(stream, filePath);

        if (result is Success<AssemblyData> success)
        {
            _packageRepository.Save(new SavedPackage
            {
                FilePath = filePath,
                AssemblyName = success.Value.FullyQualifiedName
            });
        }

        return result;
    }

    public async Task<Result<bool>> RemoveAssemblyAsync(string fullName)
    {
        var entry = _assemblies.FirstOrDefault(la => la.Assembly.FullName == fullName);
        if (entry == null)
            return new Failure<bool>($"Assembly '{fullName}' not found.");

        _assemblies.Remove(entry);
        // Publish before Unload so consumers drop references while the context is still alive.
        await _bus.PublishAsync(new AssemblyUnloaded(fullName));
        entry.Context.Unload();

        var saved = _packageRepository.GetAll().FirstOrDefault(p => p.AssemblyName == fullName);
        if (saved != null)
            _packageRepository.Delete(saved.FilePath);

        return new Success<bool>(true);
    }

    private async Task<Result<AssemblyData>> LoadAssemblyFromStreamAsync(Stream stream, string? assemblyPath)
    {
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        var loaded = await _assemblyLoader.LoadAssemblyAsync(memoryStream, assemblyPath);

        if (loaded == null)
            return new Failure<AssemblyData>("Failed to load assembly. The file may be invalid or unsupported.");

        // Mapping is what actually walks the assembly's types, so it is where a dependency the
        // load context could not find finally surfaces. Do it before the entry is kept and
        // announced: an assembly that cannot be mapped once cannot be mapped later either, and
        // keeping it would make every subsequent GetLoadedAssembliesAsync rethrow and leave the
        // Packages page broken until restart.
        AssemblyData contract;
        try
        {
            contract = loaded.Assembly.ToContract(_settings.MaxTypeDepth);
        }
        catch (Exception e)
        {
            loaded.Context.Unload();
            return new Failure<AssemblyData>(DescribeMappingFailure(e));
        }

        // Unload existing assembly with same FullName (reload support)
        var existing = _assemblies.FirstOrDefault(la => la.Assembly.FullName == loaded.Assembly.FullName);
        if (existing != null)
        {
            _assemblies.Remove(existing);
            // Publish before Unload so consumers drop references while the context is still alive.
            await _bus.PublishAsync(new AssemblyUnloaded(existing.Assembly.FullName ?? string.Empty));
            existing.Context.Unload();
        }

        _assemblies.Add(loaded);
        await _bus.PublishAsync(new AssemblyLoaded(loaded));

        return new Success<AssemblyData>(contract);
    }

    /// <summary>
    /// Names the assembly that is actually missing where the runtime knows it, because
    /// "copy the DLL it needs next to it" is only actionable advice if the user is told which.
    /// </summary>
    private static string DescribeMappingFailure(Exception e)
    {
        var missing = e switch
        {
            ReflectionTypeLoadException rtle => rtle.LoaderExceptions
                .OfType<FileNotFoundException>()
                .Select(x => x.FileName)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
            FileNotFoundException fnf => fnf.FileName,
            _ => null
        };

        return missing is null
            ? $"Loaded the assembly but could not read its types: {e.Message}"
            : $"Loaded the assembly but could not read its types because '{missing}' is missing. Copy it next to the assembly and try again.";
    }

    public void Dispose()
    {
        foreach (var entry in _assemblies)
        {
            entry.Context.Unload();
        }
        _assemblies.Clear();
    }
}
