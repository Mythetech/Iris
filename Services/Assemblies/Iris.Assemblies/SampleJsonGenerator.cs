using System.Text.Json.Nodes;
using Iris.Contracts.Assemblies;
using Iris.Contracts.Assemblies.Models;

namespace Iris.Assemblies;

public class SampleJsonGenerator : ISampleJsonGenerator
{
    public JsonNode GenerateSample(TypeData typeData)
    {
        var obj = new JsonObject();

        if (typeData.Properties == null)
            return obj;

        foreach (var prop in typeData.Properties)
        {
            obj[prop.Name] = GenerateValue(prop);
        }

        return obj;
    }

    private static JsonNode? GenerateValue(PropertyData prop)
    {
        return prop.Kind switch
        {
            TypeKind.Primitive => GeneratePrimitive(prop.TypeName),
            TypeKind.Enum => GenerateEnum(prop),
            TypeKind.Collection => GenerateCollection(prop),
            TypeKind.Dictionary => GenerateDictionary(prop),
            TypeKind.Complex => GenerateComplex(prop),
            _ => JsonValue.Create((string?)null)
        };
    }

    private static JsonNode GeneratePrimitive(string typeName)
    {
        return typeName switch
        {
            "String" => JsonValue.Create("sample-string"),
            "Guid" => JsonValue.Create(Guid.NewGuid().ToString()),
            "DateTime" or "DateTimeOffset" => JsonValue.Create(DateTime.UtcNow.ToString("O")),
            "DateOnly" => JsonValue.Create(DateOnly.FromDateTime(DateTime.UtcNow).ToString("O")),
            "TimeOnly" or "TimeSpan" => JsonValue.Create("00:00:00"),
            "Boolean" => JsonValue.Create(true),
            "Byte" or "SByte" or "Int16" or "UInt16" or "Int32" or "UInt32" or "Int64" or "UInt64" => JsonValue.Create(1),
            "Single" or "Double" or "Decimal" => JsonValue.Create(1.0),
            "Char" => JsonValue.Create("A"),
            _ => JsonValue.Create("sample-string")
        };
    }

    private static JsonNode GenerateEnum(PropertyData prop)
    {
        if (prop.EnumValues is { Count: > 0 })
            return JsonValue.Create(prop.EnumValues[0]);
        return JsonValue.Create(prop.TypeName);
    }

    private static JsonNode GenerateCollection(PropertyData prop)
    {
        var arr = new JsonArray();
        if (prop.Children is { Count: > 0 })
        {
            arr.Add(GenerateValue(prop.Children[0]));
        }
        return arr;
    }

    private static JsonNode GenerateDictionary(PropertyData prop)
    {
        var obj = new JsonObject();
        if (prop.Children is { Count: >= 2 })
        {
            obj["key"] = GenerateValue(prop.Children[1]); // value type
        }
        return obj;
    }

    private static JsonNode GenerateComplex(PropertyData prop)
    {
        var obj = new JsonObject();
        if (prop.Children == null)
            return obj;
        foreach (var child in prop.Children)
        {
            obj[child.Name] = GenerateValue(child);
        }
        return obj;
    }
}
