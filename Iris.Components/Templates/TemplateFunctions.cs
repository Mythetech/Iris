namespace Iris.Components.Templates;

/// <summary>
/// A precanned template function that can be invoked to produce a concrete value.
/// </summary>
public record TemplateFunction(
    string Name,
    string Description,
    string[] CompatibleTypes,
    Func<string[], string> Invoke);

/// <summary>
/// Registry of precanned template functions for resolving <c>{{FunctionName()}}</c> expressions.
/// </summary>
public static class TemplateFunctions
{
    private static readonly Dictionary<string, TemplateFunction> _functions = new()
    {
        ["NewGuid"] = new TemplateFunction(
            Name: "NewGuid",
            Description: "Generates a new random GUID.",
            CompatibleTypes: ["Guid", "string"],
            Invoke: _ => Guid.NewGuid().ToString()),

        ["Now"] = new TemplateFunction(
            Name: "Now",
            Description: "Returns the current local date and time as an ISO-8601 string.",
            CompatibleTypes: ["DateTime", "DateTimeOffset", "string"],
            Invoke: _ => DateTimeOffset.Now.ToString("o")),

        ["UtcNow"] = new TemplateFunction(
            Name: "UtcNow",
            Description: "Returns the current UTC date and time as an ISO-8601 string.",
            CompatibleTypes: ["DateTime", "DateTimeOffset", "string"],
            Invoke: _ => DateTimeOffset.UtcNow.ToString("o")),

        ["RandomInt"] = new TemplateFunction(
            Name: "RandomInt",
            Description: "Generates a random integer. Args: min (default 0), max (default 1000).",
            CompatibleTypes: ["int", "long", "decimal", "double", "float", "string"],
            Invoke: args =>
            {
                int min = args.Length > 0 && int.TryParse(args[0], out var lo) ? lo : 0;
                int max = args.Length > 1 && int.TryParse(args[1], out var hi) ? hi : 1000;
                return Random.Shared.Next(min, max).ToString();
            }),

        ["RandomString"] = new TemplateFunction(
            Name: "RandomString",
            Description: "Generates a random alphanumeric string. Args: length (default 8).",
            CompatibleTypes: ["string"],
            Invoke: args =>
            {
                int length = args.Length > 0 && int.TryParse(args[0], out var len) ? len : 8;
                const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
                return new string(Enumerable.Range(0, length)
                    .Select(_ => chars[Random.Shared.Next(chars.Length)])
                    .ToArray());
            }),

        // TODO: Enable in phase 2 when resolver moves to send pipeline
        // ["Increment"] = new TemplateFunction(
        //     Name: "Increment",
        //     Description: "Increments a counter each time a message is sent.",
        //     CompatibleTypes: ["int", "long", "string"],
        //     Invoke: _ => throw new NotImplementedException("Increment is not yet supported.")),
    };

    /// <summary>
    /// Returns all registered template functions.
    /// </summary>
    public static IReadOnlyDictionary<string, TemplateFunction> GetAvailableFunctions()
        => _functions;

    /// <summary>
    /// Invokes the named function with the supplied arguments.
    /// Returns <c>{{functionName()}}</c> for unknown function names.
    /// </summary>
    public static string Invoke(string functionName, params string[] args)
    {
        if (_functions.TryGetValue(functionName, out var function))
            return function.Invoke(args);

        return $"{{{{{functionName}()}}}}";
    }

    /// <summary>
    /// Returns all functions that declare compatibility with the given type name.
    /// </summary>
    public static IReadOnlyDictionary<string, TemplateFunction> GetCompatibleFunctions(string typeName)
        => _functions
            .Where(kvp => kvp.Value.CompatibleTypes.Contains(typeName))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
}
