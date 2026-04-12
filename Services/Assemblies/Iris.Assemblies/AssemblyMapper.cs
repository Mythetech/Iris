using System.Reflection;
using Iris.Contracts.Assemblies.Models;

namespace Iris.Assemblies;

public static class AssemblyMapper
{
    public static AssemblyData ToContract(this Assembly asm, int maxDepth = 3)
    {
        return new AssemblyData
        {
            Name = asm.GetName().Name ?? "",
            ExportedTypeNames = asm.GetExportedTypes().Select(t => t.Name).ToList(),
            ExportedTypes = asm.GetExportedTypes().Select(t => t.ToContract(maxDepth)).ToList(),
            FullyQualifiedName = asm.FullName ?? "",
            Version = asm.GetName().Version?.ToString() ?? ""
        };
    }
}