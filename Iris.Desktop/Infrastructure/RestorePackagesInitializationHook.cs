using Iris.Components.PackageManagement;
using Iris.Contracts.Assemblies.Models;
using Iris.Contracts.Results;
using Iris.Desktop.PackageManagement;
using Microsoft.Extensions.Logging;
using Mythetech.Framework.Infrastructure.Initialization;

namespace Iris.Desktop.Infrastructure;

public class RestorePackagesInitializationHook : IAsyncInitializationHook
{
    private readonly PackageRepository _packageRepository;
    private readonly IPackageService _packageService;
    private readonly ILogger<RestorePackagesInitializationHook> _logger;

    public RestorePackagesInitializationHook(
        PackageRepository packageRepository,
        IPackageService packageService,
        ILogger<RestorePackagesInitializationHook> logger)
    {
        _packageRepository = packageRepository;
        _packageService = packageService;
        _logger = logger;
    }

    public int Order => 700;

    public string Name => "Restore Packages";

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var saved = _packageRepository.GetAll();

        foreach (var entry in saved)
        {
            if (!File.Exists(entry.FilePath))
            {
                _logger.LogWarning("Saved package path no longer exists: {Path}, removing entry", entry.FilePath);
                _packageRepository.DeleteById(entry.Id);
                continue;
            }

            try
            {
                var result = await _packageService.UploadAssemblyAsync(entry.FilePath);

                if (result is Success<AssemblyData>)
                {
                    _logger.LogInformation("Restored package {Name} from {Path}", entry.AssemblyName, entry.FilePath);
                }
                else
                {
                    _logger.LogWarning("Failed to restore package from {Path}, removing entry", entry.FilePath);
                    _packageRepository.DeleteById(entry.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to restore package from {Path}, skipping", entry.FilePath);
            }
        }
    }
}
