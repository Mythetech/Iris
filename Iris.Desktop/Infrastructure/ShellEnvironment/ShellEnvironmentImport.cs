namespace Iris.Desktop.Infrastructure.ShellEnvironment;

public static class ShellEnvironmentImport
{
    private const string AwsPrefix = "AWS_";
    private const string PathVariable = "PATH";
    private const char PathSeparator = ':';

    /// <summary>
    /// The variables to copy from the login shell into this process. Only what the AWS SDK
    /// reads is taken: its own <c>AWS_</c> variables, and <c>PATH</c> so that a profile's
    /// credential_process helper can be found. The process's own values always win, so Iris
    /// started from a terminal keeps what that terminal exported.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Select(
        IReadOnlyDictionary<string, string> current,
        IReadOnlyDictionary<string, string> shell)
    {
        var selected = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (name, value) in shell)
        {
            if (!name.StartsWith(AwsPrefix, StringComparison.Ordinal) || string.IsNullOrEmpty(value))
                continue;

            if (current.TryGetValue(name, out var existing) && !string.IsNullOrEmpty(existing))
                continue;

            selected[name] = value;
        }

        if (shell.TryGetValue(PathVariable, out var shellPath))
        {
            var merged = MergePath(current.GetValueOrDefault(PathVariable), shellPath);

            if (merged is not null)
                selected[PathVariable] = merged;
        }

        return selected;
    }

    private static string? MergePath(string? currentPath, string shellPath)
    {
        var entries = (currentPath ?? string.Empty).Split(PathSeparator, StringSplitOptions.RemoveEmptyEntries).ToList();
        var known = new HashSet<string>(entries, StringComparer.Ordinal);
        var added = false;

        foreach (var entry in shellPath.Split(PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!known.Add(entry))
                continue;

            entries.Add(entry);
            added = true;
        }

        return added ? string.Join(PathSeparator, entries) : null;
    }
}
