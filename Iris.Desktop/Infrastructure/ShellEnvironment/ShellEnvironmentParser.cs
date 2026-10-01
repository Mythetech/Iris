using System.Text.RegularExpressions;

namespace Iris.Desktop.Infrastructure.ShellEnvironment;

public static partial class ShellEnvironmentParser
{
    /// <summary>
    /// Reads the output of <c>env</c>. An interactive login shell can print anything around
    /// it, so a line counts only when it starts with a valid variable name and an equals sign.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Parse(string? text)
    {
        var variables = new Dictionary<string, string>(StringComparer.Ordinal);

        if (string.IsNullOrEmpty(text))
            return variables;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var separator = line.IndexOf('=');

            if (separator <= 0)
                continue;

            var name = line[..separator];

            if (VariableName().IsMatch(name))
                variables[name] = line[(separator + 1)..];
        }

        return variables;
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex VariableName();
}
