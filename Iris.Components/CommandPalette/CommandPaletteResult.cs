namespace Iris.Components.CommandPalette;

/// <summary>
/// Result returned from <see cref="CommandPaletteService.GetCommandsAsync"/>.
/// </summary>
/// <param name="Commands">The filtered, ranked commands to display.</param>
/// <param name="HasProviderErrors">
/// True if at least one provider threw and was excluded. The UI surfaces this as a
/// "Some commands unavailable" warning so the palette degrades gracefully when a single
/// provider misbehaves.
/// </param>
public sealed record CommandPaletteResult(
    IReadOnlyList<PaletteCommand> Commands,
    bool HasProviderErrors);
