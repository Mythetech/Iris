using Iris.Assemblies;
using Iris.Components.PackageManagement;
using Iris.Contracts.Assemblies.Models;
using Iris.Contracts.Results;
using Microsoft.AspNetCore.Components.Forms;

namespace Iris.Desktop.PackageManagement;

public class LocalPackageService : IPackageService, IDisposable
{
    private readonly IAssemblyLoadService _assemblyLoader;
    private readonly AssemblySettings _settings;
    private readonly PackageRepository _packageRepository;
    private List<LoadedAssembly> _assemblies = [];

    public LocalPackageService(IAssemblyLoadService assemblyLoader, AssemblySettings settings, PackageRepository packageRepository)
    {
        _assemblyLoader = assemblyLoader;
        _settings = settings;
        _packageRepository = packageRepository;
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
        return await LoadAssemblyFromStreamAsync(stream);
    }

    public async Task<Result<AssemblyData>> UploadAssemblyAsync(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        var result = await LoadAssemblyFromStreamAsync(stream);

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

    public Task<Result<bool>> RemoveAssemblyAsync(string fullName)
    {
        var entry = _assemblies.FirstOrDefault(la => la.Assembly.FullName == fullName);
        if (entry == null)
            return Task.FromResult<Result<bool>>(new Failure<bool>($"Assembly '{fullName}' not found."));

        _assemblies.Remove(entry);
        entry.Context.Unload();

        var saved = _packageRepository.GetAll().FirstOrDefault(p => p.AssemblyName == fullName);
        if (saved != null)
            _packageRepository.Delete(saved.FilePath);

        return Task.FromResult<Result<bool>>(new Success<bool>(true));
    }

    private async Task<Result<AssemblyData>> LoadAssemblyFromStreamAsync(Stream stream)
    {
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        var loaded = await _assemblyLoader.LoadAssemblyAsync(memoryStream);

        if (loaded == null)
            return new Failure<AssemblyData>("Failed to load assembly. The file may be invalid or unsupported.");

        // Unload existing assembly with same FullName (reload support)
        var existing = _assemblies.FirstOrDefault(la => la.Assembly.FullName == loaded.Assembly.FullName);
        if (existing != null)
        {
            _assemblies.Remove(existing);
            existing.Context.Unload();
        }

        _assemblies.Add(loaded);

        return new Success<AssemblyData>(loaded.Assembly.ToContract(_settings.MaxTypeDepth));
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
