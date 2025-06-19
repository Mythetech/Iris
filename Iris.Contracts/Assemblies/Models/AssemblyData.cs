using System;
namespace Iris.Contracts.Assemblies.Models
{
    public class AssemblyData
    {
        public string Name { get; set; } = "";

        public string FullyQualifiedName { get; set; } = "";

        public List<string>? ExportedTypeNames { get; set; }

        public string Version { get; set; } = "";

        public List<TypeData>? ExportedTypes { get; set; }
    }
}

