using Microsoft.Extensions.Logging;

namespace Iris.Components.CommandPalette;

/// <summary>
/// Aggregates commands from all registered <see cref="ICommandProvider"/> instances,
/// applies the user's search query, and returns ranked results for the palette UI.
///
/// A single failing provider does not break the palette: its exception is logged and
/// excluded, the surviving providers are returned, and <see cref="CommandPaletteResult.HasProviderErrors"/>
/// is set so the UI can surface a degraded-state warning.
/// </summary>
public sealed class CommandPaletteService
{
    private readonly IReadOnlyList<ICommandProvider> _providers;
    private readonly ILogger<CommandPaletteService> _logger;

    public CommandPaletteService(
        IEnumerable<ICommandProvider> providers,
        ILogger<CommandPaletteService> logger)
    {
        _providers = providers.ToArray();
        _logger = logger;
    }

    /// <summary>
    /// True while the palette dialog is on-screen. The hotkey host uses this to
    /// avoid stacking dialogs (and, optionally, to toggle the palette closed on a
    /// second hotkey press).
    /// </summary>
    public bool IsOpen { get; private set; }

    public void MarkOpened() => IsOpen = true;
    public void MarkClosed() => IsOpen = false;

    public async Task<CommandPaletteResult> GetCommandsAsync(string query, CancellationToken ct)
    {
        var hasErrors = false;
        var all = new List<PaletteCommand>();

        foreach (var provider in _providers)
        {
            try
            {
                var commands = await provider.GetCommandsAsync(ct).ConfigureAwait(false);
                all.AddRange(commands);
            }
            catch (Exception ex)
            {
                hasErrors = true;
                _logger.LogError(ex,
                    "Command provider {Provider} threw while loading palette commands; excluding its results",
                    provider.GetType().FullName);
            }
        }

        var filtered = Filter(all, query);
        return new CommandPaletteResult(filtered, hasErrors);
    }

    private static IReadOnlyList<PaletteCommand> Filter(IReadOnlyList<PaletteCommand> commands, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return commands;
        }

        var trimmed = query.Trim();

        var ranked = new List<(int rank, int order, PaletteCommand command)>();
        for (var i = 0; i < commands.Count; i++)
        {
            var cmd = commands[i];
            var rank = Score(cmd, trimmed);
            if (rank == NoMatch)
            {
                continue;
            }

            ranked.Add((rank, i, cmd));
        }

        return ranked
            .OrderBy(x => x.rank)
            .ThenBy(x => x.order)
            .Select(x => x.command)
            .ToArray();
    }

    private const int RankTitlePrefix = 0;
    private const int RankTitleSubstring = 1;
    private const int RankDescriptionOrKeyword = 2;
    private const int NoMatch = int.MaxValue;

    private static int Score(PaletteCommand cmd, string query)
    {
        if (cmd.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return RankTitlePrefix;
        }

        if (cmd.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return RankTitleSubstring;
        }

        if (cmd.Description is not null &&
            cmd.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return RankDescriptionOrKeyword;
        }

        foreach (var keyword in cmd.Keywords)
        {
            if (keyword.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                return RankDescriptionOrKeyword;
            }
        }

        return NoMatch;
    }
}
