using System.Reflection;
using Iris.Contracts.Assemblies.Models;

namespace Iris.Assemblies;

public static class AssemblyMapper
{
    public static AssemblyData ToContract(this Assembly asm)
    {
        return new AssemblyData()
        {
            Name = asm.GetName().Name,
            ExportedTypeNames = asm.GetExportedTypes().Select(t => t.Name).ToList(),
            ExportedTypes = asm.GetExportedTypes().Select(t => t.ToContract()).ToList(),
            FullyQualifiedName = asm.FullName,
            Version = asm.GetName().Version.ToString(),
        };
    }
}