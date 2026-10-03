using System.Collections;
using Microsoft.Extensions.Logging;
using Mythetech.Framework.Infrastructure.Initialization;

namespace Iris.Desktop.Infrastructure.ShellEnvironment;

public class ShellEnvironmentInitializationHook : IAsyncInitializationHook
{
    private readonly LoginShellEnvironmentReader _reader;
    private readonly ILogger<ShellEnvironmentInitializationHook> _logger;

    public ShellEnvironmentInitializationHook(
        LoginShellEnvironmentReader reader,
        ILogger<ShellEnvironmentInitializationHook> logger)
    {
        _reader = reader;
        _logger = logger;
    }

    // Before RestoreConnectionsInitializationHook (500): a saved profile or default-chain
    // connection has to restore with the imported environment already in place.
    public int Order => 400;

    public string Name => "Shell Environment";

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Windows GUI apps inherit the user's environment, so there is nothing to import. A
        // terminal on standard input means Iris was started from a shell and already has its
        // environment, and an interactive child shell would contend for that terminal.
        if (!OperatingSystem.IsWindows() && Console.IsInputRedirected)
        {
            var shell = ShellEnvironmentParser.Parse(await _reader.ReadAsync(cancellationToken));
            Apply(ShellEnvironmentImport.Select(Current(), shell));
        }

        // After the import, so a value from the shell counts as already decided.
        Apply(AwsEnvironmentDefaults.Select(Current()));
    }

    private void Apply(IReadOnlyDictionary<string, string> variables)
    {
        if (variables.Count == 0)
            return;

        foreach (var (name, value) in variables)
            Environment.SetEnvironmentVariable(name, value);

        // Names only. Several of these hold credentials.
        _logger.LogInformation("Set environment variables: {Names}", string.Join(", ", variables.Keys.Order(StringComparer.Ordinal)));
    }

    private static Dictionary<string, string> Current()
    {
        // Windows treats variable names case-insensitively, and a second name differing only
        // in case would overwrite the first when set.
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var current = new Dictionary<string, string>(comparer);

        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            current[(string)entry.Key] = entry.Value as string ?? string.Empty;

        return current;
    }
}
