using MudBlazor;

namespace Iris.Components.Shared.Snackbar;

public static class SnackbarExtensions
{
    public static void AddIrisNotification(this ISnackbar snackbar, string message, Severity severity = Severity.Info)
    {
        var parameters = new Dictionary<string, object>
        {
            { "Message", message },
            {"Severity", severity}
        };

        snackbar.Add<IrisSnackbar>(parameters, severity);
    }
}