using Microsoft.JSInterop;
using Mythetech.Framework.Infrastructure.Guards;
using Mythetech.Framework.Infrastructure.Smoke;

namespace Iris.Desktop.Smoke;

/// <summary>
/// Passes when Monaco and the Chart.js scripts both loaded in the WebView. Neither renders on the home page
/// of a fresh install: the message editor is behind a navigation and the charts need a connection. A publish
/// that drops or clobbers one of them, as the Chart.js publish layout once did, would otherwise pass the
/// smoke run and break the first time someone opened the editor or connected a broker.
/// </summary>
public sealed class ScriptsSmokeCheck(IJSRuntime js, IJsGuardService guards) : ISmokeCheck
{
    private static readonly TimeSpan MonacoTimeout = TimeSpan.FromSeconds(8);

    public string Name => "iris/scripts";

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (!await guards.WaitForReadyAsync(js, "monaco", MonacoTimeout))
            throw new InvalidOperationException("Monaco did not load, so the message editor cannot open");

        string[] missing;
        try
        {
            missing = await js.InvokeAsync<string[]>("irisSmoke.missingChartScripts", cancellationToken);
        }
        catch (JSException ex)
        {
            throw new InvalidOperationException("The Iris smoke helper in index.html did not load", ex);
        }

        if (missing.Length > 0)
            throw new InvalidOperationException($"Chart.js scripts did not load: {string.Join(", ", missing)}");
    }
}
