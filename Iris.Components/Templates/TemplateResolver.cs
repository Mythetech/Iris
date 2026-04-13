using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Iris.Components.Templates;

/// <summary>
/// Walks a JSON string and replaces <c>{{FunctionName(args?)}}</c> expressions
/// with concrete values produced by <see cref="TemplateFunctions"/>.
/// </summary>
public partial class TemplateResolver : ITemplateResolver
{
    [GeneratedRegex(@"\{\{(\w+)\((.*?)\)\}\}")]
    private static partial Regex ExpressionPattern();

    public Task<string?> ResolveAsync(string? templateJson)
    {
        if (templateJson is null)
            return Task.FromResult<string?>(null);

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(templateJson);
        }
        catch (Exception)
        {
            return Task.FromResult(templateJson);
        }

        if (root is null)
            return Task.FromResult(templateJson);

        WalkNode(root);

        return Task.FromResult(root.ToJsonString());
    }

    private static void WalkNode(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(kvp => kvp.Key).ToList())
                {
                    var child = obj[key];
                    if (child is JsonValue val && TryResolveString(val, out var resolved))
                        obj[key] = resolved;
                    else if (child is not null)
                        WalkNode(child);
                }
                break;

            case JsonArray arr:
                for (int i = 0; i < arr.Count; i++)
                {
                    var child = arr[i];
                    if (child is JsonValue val && TryResolveString(val, out var resolved))
                        arr[i] = resolved;
                    else if (child is not null)
                        WalkNode(child);
                }
                break;
        }
    }

    private static bool TryResolveString(JsonValue value, out string? resolved)
    {
        resolved = null;

        if (!value.TryGetValue<string>(out var str) || str is null)
            return false;

        var result = ExpressionPattern().Replace(str, match =>
        {
            var functionName = match.Groups[1].Value;
            var rawArgs = match.Groups[2].Value;
            var args = string.IsNullOrWhiteSpace(rawArgs)
                ? []
                : rawArgs.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            return TemplateFunctions.Invoke(functionName, args);
        });

        if (result == str)
            return false;

        resolved = result;
        return true;
    }
}
