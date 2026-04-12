namespace Iris.Contracts.Assemblies.Models
{
    public class TypeData
    {
        public string Name { get; set; } = "";

        public string FullyQualifiedName { get; set; } = "";

        public List<PropertyData>? Properties { get; set; }
    }
}

