using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Iris.Contracts.Assemblies.Models;

namespace Iris.Components.Templates;

/// <summary>
/// Editing model that bridges <see cref="PropertyData"/> (from TypeMapper) to the form builder UI.
/// Each field in the template editor is represented by one <see cref="TemplateFieldModel"/>.
/// </summary>
public partial class TemplateFieldModel
{
    // ── Expression detection regex ─────────────────────────────────────────────
    [GeneratedRegex(@"^\{\{(\w+\(.*?\))\}\}$")]
    private static partial Regex ExpressionPattern();

    // ── Properties ────────────────────────────────────────────────────────────

    public string Name { get; set; } = "";
    public string TypeName { get; set; } = "";
    public TypeKind Kind { get; set; }

    /// <summary>Static value; null when an expression is set.</summary>
    public object? Value { get; set; }

    /// <summary>Expression such as <c>NewGuid()</c>; null when a static value is set.</summary>
    public string? Expression { get; set; }

    public List<string>? EnumValues { get; set; }
    public List<string>? GenericArguments { get; set; }

    /// <summary>Child fields for nested complex objects.</summary>
    public List<TemplateFieldModel>? Children { get; set; }

    /// <summary>Array items for complex collections — each item is a list of fields.</summary>
    public List<List<TemplateFieldModel>>? Items { get; set; }

    /// <summary>Array items for primitive collections such as <c>List&lt;string&gt;</c>.</summary>
    public List<string>? PrimitiveItems { get; set; }

    /// <summary>True when <see cref="Expression"/> is set (non-null, non-empty).</summary>
    public bool HasExpression => !string.IsNullOrEmpty(Expression);

    // ── FromPropertyData ──────────────────────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="TemplateFieldModel"/> from a <see cref="PropertyData"/> descriptor,
    /// recursively mapping children for complex types.
    /// </summary>
    public static TemplateFieldModel FromPropertyData(PropertyData prop)
    {
        var model = new TemplateFieldModel
        {
            Name = prop.Name,
            TypeName = prop.TypeName,
            Kind = prop.Kind,
            EnumValues = prop.EnumValues is { Count: > 0 } ? [.. prop.EnumValues] : null,
            GenericArguments = prop.GenericArguments is { Count: > 0 } ? [.. prop.GenericArguments] : null,
        };

        switch (prop.Kind)
        {
            case TypeKind.Enum:
                model.Value = prop.EnumValues?.FirstOrDefault();
                break;

            case TypeKind.Complex when prop.Children is { Count: > 0 }:
                model.Children = prop.Children.Select(FromPropertyData).ToList();
                break;

            case TypeKind.Collection when prop.Children is { Count: > 0 }:
                // Propagate child schema but leave Items/PrimitiveItems empty (no data yet)
                model.Children = prop.Children.Select(FromPropertyData).ToList();
                break;
        }

        return model;
    }

    // ── ToJson ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Serializes a list of <see cref="TemplateFieldModel"/> to a JSON object string.
    /// Expressions are emitted as <c>"{{Expression}}"</c>.
    /// </summary>
    public static string ToJson(List<TemplateFieldModel> fields)
    {
        var obj = BuildJsonObject(fields);
        return obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static JsonObject BuildJsonObject(List<TemplateFieldModel> fields)
    {
        var obj = new JsonObject();

        foreach (var field in fields)
            obj[field.Name] = BuildJsonValue(field);

        return obj;
    }

    private static JsonNode? BuildJsonValue(TemplateFieldModel field)
    {
        if (field.HasExpression)
            return JsonValue.Create($"{{{{{field.Expression}}}}}");

        return field.Kind switch
        {
            TypeKind.Complex when field.Children is { Count: > 0 }
                => BuildJsonObject(field.Children),

            TypeKind.Collection when field.PrimitiveItems is { Count: > 0 }
                => BuildPrimitiveArray(field.PrimitiveItems),

            TypeKind.Collection when field.Items is { Count: > 0 }
                => BuildComplexArray(field.Items),

            TypeKind.Collection
                => new JsonArray(),

            _ => BuildPrimitiveJsonValue(field.Value)
        };
    }

    private static JsonArray BuildPrimitiveArray(List<string> items)
    {
        var arr = new JsonArray();
        foreach (var item in items)
            arr.Add(JsonValue.Create(item));
        return arr;
    }

    private static JsonArray BuildComplexArray(List<List<TemplateFieldModel>> items)
    {
        var arr = new JsonArray();
        foreach (var itemFields in items)
            arr.Add(BuildJsonObject(itemFields));
        return arr;
    }

    private static JsonNode? BuildPrimitiveJsonValue(object? value)
    {
        if (value is null)
            return null;

        return value switch
        {
            bool b      => JsonValue.Create(b),
            int i       => JsonValue.Create(i),
            long l      => JsonValue.Create(l),
            double d    => JsonValue.Create(d),
            float f     => JsonValue.Create(f),
            decimal m   => JsonValue.Create(m),
            string s    => JsonValue.Create(s),
            _           => JsonValue.Create(value.ToString())
        };
    }

    // ── FromJson ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Deserializes a JSON object string back to a list of <see cref="TemplateFieldModel"/>.
    /// Strings matching <c>{{FunctionName(...)}}</c> are detected and their <see cref="Expression"/> is set.
    /// </summary>
    public static List<TemplateFieldModel> FromJson(string json)
    {
        var node = JsonNode.Parse(json);

        if (node is not JsonObject root)
            return [];

        return ParseJsonObject(root);
    }

    private static List<TemplateFieldModel> ParseJsonObject(JsonObject obj)
    {
        var fields = new List<TemplateFieldModel>();

        foreach (var kvp in obj)
        {
            var field = ParseJsonNode(kvp.Key, kvp.Value);
            fields.Add(field);
        }

        return fields;
    }

    private static TemplateFieldModel ParseJsonNode(string name, JsonNode? node)
    {
        if (node is null)
        {
            return new TemplateFieldModel
            {
                Name = name,
                TypeName = "",
                Kind = TypeKind.Primitive,
                Value = null
            };
        }

        if (node is JsonObject nestedObj)
        {
            return new TemplateFieldModel
            {
                Name = name,
                TypeName = "",
                Kind = TypeKind.Complex,
                Children = ParseJsonObject(nestedObj)
            };
        }

        if (node is JsonArray arr)
        {
            return ParseArrayNode(name, arr);
        }

        if (node is JsonValue jsonValue)
        {
            return ParseValueNode(name, jsonValue);
        }

        return new TemplateFieldModel { Name = name, TypeName = "" };
    }

    private static TemplateFieldModel ParseArrayNode(string name, JsonArray arr)
    {
        var model = new TemplateFieldModel
        {
            Name = name,
            TypeName = "",
            Kind = TypeKind.Collection,
        };

        if (arr.Count == 0)
            return model;

        // Determine element type from first element
        var first = arr[0];

        if (first is JsonObject)
        {
            // Complex collection
            model.Items = arr
                .OfType<JsonObject>()
                .Select(ParseJsonObject)
                .ToList();
        }
        else
        {
            // Primitive collection — collect as strings
            model.PrimitiveItems = arr
                .Select(item => item?.GetValue<object>()?.ToString() ?? "")
                .ToList();
        }

        return model;
    }

    private static TemplateFieldModel ParseValueNode(string name, JsonValue jsonValue)
    {
        var model = new TemplateFieldModel
        {
            Name = name,
            TypeName = "",
            Kind = TypeKind.Primitive,
        };

        // Try string first so we can check for expression pattern
        if (jsonValue.TryGetValue<string>(out var str) && str is not null)
        {
            var match = ExpressionPattern().Match(str);
            if (match.Success)
            {
                model.Expression = match.Groups[1].Value;
            }
            else
            {
                model.Value = str;
            }

            return model;
        }

        // Non-string primitives
        if (jsonValue.TryGetValue<bool>(out var boolVal))
        {
            model.Value = boolVal;
        }
        else if (jsonValue.TryGetValue<long>(out var longVal))
        {
            model.Value = longVal;
        }
        else if (jsonValue.TryGetValue<double>(out var doubleVal))
        {
            model.Value = doubleVal;
        }
        else if (jsonValue.TryGetValue<decimal>(out var decVal))
        {
            model.Value = decVal;
        }
        else
        {
            model.Value = jsonValue.GetValue<object>();
        }

        return model;
    }
}
