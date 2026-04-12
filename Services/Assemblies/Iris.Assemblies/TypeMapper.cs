using System.Reflection;
using Iris.Contracts.Assemblies.Models;

namespace Iris.Assemblies;

public static class TypeMapper
{
    private static readonly HashSet<Type> PrimitiveTypes = new()
    {
        typeof(string), typeof(char),
        typeof(bool),
        typeof(byte), typeof(sbyte),
        typeof(short), typeof(ushort),
        typeof(int), typeof(uint),
        typeof(long), typeof(ulong),
        typeof(float), typeof(double), typeof(decimal),
        typeof(Guid),
        typeof(DateTime), typeof(DateTimeOffset), typeof(DateOnly), typeof(TimeOnly), typeof(TimeSpan)
    };

    public static TypeData ToContract(this Type t, int maxDepth = 3)
    {
        var visited = new HashSet<Type> { t };
        return new TypeData
        {
            Name = t.Name,
            FullyQualifiedName = t.FullName ?? "",
            Properties = MapProperties(t, 0, maxDepth, visited)
        };
    }

    private static List<PropertyData> MapProperties(Type type, int currentDepth, int maxDepth, HashSet<Type> visited)
    {
        var properties = new List<PropertyData>();
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            properties.Add(MapProperty(prop, currentDepth, maxDepth, visited));
        }
        return properties;
    }

    private static PropertyData MapProperty(PropertyInfo prop, int currentDepth, int maxDepth, HashSet<Type> visited)
    {
        var propertyType = prop.PropertyType;
        var isNullable = false;

        var underlying = Nullable.GetUnderlyingType(propertyType);
        if (underlying != null)
        {
            propertyType = underlying;
            isNullable = true;
        }

        var data = new PropertyData
        {
            Name = prop.Name,
            TypeName = GetReadableTypeName(propertyType),
            Kind = ClassifyType(propertyType),
            IsNullable = isNullable
        };

        switch (data.Kind)
        {
            case TypeKind.Enum:
                data.EnumValues = Enum.GetNames(propertyType).ToList();
                break;

            case TypeKind.Collection:
                var elementType = GetCollectionElementType(propertyType);
                if (elementType != null)
                {
                    data.GenericArguments = new List<string> { GetReadableTypeName(elementType) };
                    data.Children = new List<PropertyData>
                    {
                        MapTypeAsPropertyData(elementType, "[element]", currentDepth + 1, maxDepth, visited)
                    };
                }
                break;

            case TypeKind.Dictionary:
                var (keyType, valueType) = GetDictionaryTypes(propertyType);
                if (keyType != null && valueType != null)
                {
                    data.GenericArguments = new List<string>
                    {
                        GetReadableTypeName(keyType),
                        GetReadableTypeName(valueType)
                    };
                    data.Children = new List<PropertyData>
                    {
                        MapTypeAsPropertyData(keyType, "[key]", currentDepth + 1, maxDepth, visited),
                        MapTypeAsPropertyData(valueType, "[value]", currentDepth + 1, maxDepth, visited)
                    };
                }
                break;

            case TypeKind.Complex:
                if (currentDepth + 1 < maxDepth && !visited.Contains(propertyType))
                {
                    visited.Add(propertyType);
                    data.Children = MapProperties(propertyType, currentDepth + 1, maxDepth, visited);
                    visited.Remove(propertyType);
                }
                break;
        }

        return data;
    }

    private static PropertyData MapTypeAsPropertyData(Type type, string name, int currentDepth, int maxDepth, HashSet<Type> visited)
    {
        var kind = ClassifyType(type);
        var data = new PropertyData
        {
            Name = name,
            TypeName = GetReadableTypeName(type),
            Kind = kind
        };

        switch (kind)
        {
            case TypeKind.Enum:
                data.EnumValues = Enum.GetNames(type).ToList();
                break;

            case TypeKind.Complex:
                if (currentDepth + 1 < maxDepth && !visited.Contains(type))
                {
                    visited.Add(type);
                    data.Children = MapProperties(type, currentDepth, maxDepth, visited);
                    visited.Remove(type);
                }
                break;

            case TypeKind.Collection:
                var elementType = GetCollectionElementType(type);
                if (elementType != null)
                {
                    data.GenericArguments = new List<string> { GetReadableTypeName(elementType) };
                    data.Children = new List<PropertyData>
                    {
                        MapTypeAsPropertyData(elementType, "[element]", currentDepth + 1, maxDepth, visited)
                    };
                }
                break;

            case TypeKind.Dictionary:
                var (keyType, valueType) = GetDictionaryTypes(type);
                if (keyType != null && valueType != null)
                {
                    data.GenericArguments = new List<string>
                    {
                        GetReadableTypeName(keyType),
                        GetReadableTypeName(valueType)
                    };
                    data.Children = new List<PropertyData>
                    {
                        MapTypeAsPropertyData(keyType, "[key]", currentDepth + 1, maxDepth, visited),
                        MapTypeAsPropertyData(valueType, "[value]", currentDepth + 1, maxDepth, visited)
                    };
                }
                break;
        }

        return data;
    }

    internal static TypeKind ClassifyType(Type type)
    {
        if (PrimitiveTypes.Contains(type))
            return TypeKind.Primitive;
        if (type.IsEnum)
            return TypeKind.Enum;
        if (IsDictionaryType(type))
            return TypeKind.Dictionary;
        if (IsCollectionType(type))
            return TypeKind.Collection;
        return TypeKind.Complex;
    }

    private static bool IsCollectionType(Type type)
    {
        if (type.IsArray) return true;
        if (!type.IsGenericType) return false;
        var generic = type.GetGenericTypeDefinition();
        return generic == typeof(List<>)
               || generic == typeof(IEnumerable<>)
               || generic == typeof(ICollection<>)
               || generic == typeof(IReadOnlyCollection<>)
               || generic == typeof(IReadOnlyList<>)
               || generic == typeof(IList<>)
               || generic == typeof(HashSet<>);
    }

    private static bool IsDictionaryType(Type type)
    {
        if (!type.IsGenericType) return false;
        var generic = type.GetGenericTypeDefinition();
        return generic == typeof(Dictionary<,>)
               || generic == typeof(IDictionary<,>)
               || generic == typeof(IReadOnlyDictionary<,>);
    }

    private static Type? GetCollectionElementType(Type type)
    {
        if (type.IsArray) return type.GetElementType();
        if (type.IsGenericType) return type.GetGenericArguments().FirstOrDefault();
        return null;
    }

    private static (Type? key, Type? value) GetDictionaryTypes(Type type)
    {
        if (!type.IsGenericType) return (null, null);
        var args = type.GetGenericArguments();
        return args.Length == 2 ? (args[0], args[1]) : (null, null);
    }

    public static string GetReadableTypeName(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
            return GetReadableTypeName(underlying) + "?";
        if (!type.IsGenericType)
            return type.Name;
        var baseName = type.Name.Split('`')[0];
        var args = string.Join(", ", type.GetGenericArguments().Select(GetReadableTypeName));
        return $"{baseName}<{args}>";
    }
}
