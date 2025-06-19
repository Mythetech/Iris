using System.Reflection;
using Iris.Contracts.Assemblies.Models;

namespace Iris.Assemblies;

public static class TypeMapper
{
    public static TypeData ToContract(this Type t)
    {
        var data = new TypeData
        {
            Name = t.Name,
            FullyQualifiedName = t.FullName ?? "",
            Properties = MapProperties(t)
        };
        
        return data;
    }
    
    private static Dictionary<string, string>? MapProperties(Type type)
    {
        var propertiesDictionary = new Dictionary<string, string>();

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            // Get the property name and type
            string propertyName = property.Name;
            string propertyType = property.PropertyType.Name;

            propertiesDictionary[propertyName] = propertyType;
        }

        return propertiesDictionary;
    }
}