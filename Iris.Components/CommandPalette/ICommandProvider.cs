namespace Iris.Components.CommandPalette;

/// <summary>
/// Source of <see cref="PaletteCommand"/> instances surfaced by the command palette.
/// Called on every palette open so providers can return context-dependent commands
/// (e.g. the current set of connections, history entries, templates).
/// </summary>
public interface ICommandProvider
{
    ValueTask<IReadOnlyList<PaletteCommand>> GetCommandsAsync(CancellationToken ct);
}
