using Iris.Contracts.Assemblies.Models;
using Iris.Contracts.Brokers.Models;

namespace Iris.Components.PackageManagement;

public interface IPackageSourceService
{
    public Task<PackageSource> GetRegisteredPackageSourcesAsync();

    public Task RegisterPackageSourceAsync(ConnectionData data);
}