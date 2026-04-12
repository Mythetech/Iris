namespace Iris.Contracts.Assemblies.Models;

public class PropertyData
{
    public string Name { get; set; } = "";
    public string TypeName { get; set; } = "";
    public TypeKind Kind { get; set; }
    public List<PropertyData>? Children { get; set; }
    public List<string>? EnumValues { get; set; }
    public List<string>? GenericArguments { get; set; }
    public bool IsNullable { get; set; }
}

public enum TypeKind
{
    Primitive,
    Complex,
    Enum,
    Collection,
    Dictionary
}
