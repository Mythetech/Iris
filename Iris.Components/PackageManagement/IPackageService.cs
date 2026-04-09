using System;
using Iris.Contracts.Assemblies.Models;
using Iris.Contracts.Brokers.Models;
using Iris.Contracts.Results;
using Microsoft.AspNetCore.Components.Forms;

namespace Iris.Components.PackageManagement
{
    public interface IPackageService
    {
        public Task<List<AssemblyData>> GetLoadedAssembliesAsync();
        

        public Task<Result<AssemblyData>> UploadAssemblyAsync(IBrowserFile file);

        public Task<Result<AssemblyData>> UploadAssemblyAsync(string filePath);
    }
}

