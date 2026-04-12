using Iris.Contracts.Assemblies.Models;
using Iris.Contracts.Results;
using Microsoft.AspNetCore.Components.Forms;

namespace Iris.Components.PackageManagement;

public interface IPackageService
{
    Task<List<AssemblyData>> GetLoadedAssembliesAsync();

    Task<Result<AssemblyData>> UploadAssemblyAsync(IBrowserFile file);

    Task<Result<AssemblyData>> UploadAssemblyAsync(string filePath);

    Task<Result<bool>> RemoveAssemblyAsync(string fullName);
}
