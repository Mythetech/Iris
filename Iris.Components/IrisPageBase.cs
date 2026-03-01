using System.Diagnostics;
using System.Text.Json;
using Iris.Components.Shared.Snackbar;
using Iris.Contracts.Results;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Iris.Components
{
    public class IrisPageBase : LayoutComponentBase
    {
        [Inject]
        public NavigationManager NavigationManager { get; set; } = default!;

        [Inject]
        public ISnackbar Snackbar { get; set; } = default!;

        public virtual string Identifier { get; set; } = "";

        public void NotifyError(string message, string title = "Error")
        {
            Snackbar.Add(message, Severity.Error);
            
        }

        public void HandleResult<T>(Result<T> result, string successMessage = "Success", string error = "Error")
        {
            if (result == null || result.Error || (result.Value is bool success && !success))
            {
                try
                {
                    var apiError = JsonSerializer.Deserialize<ApiErrorResponse>(error);

                    if (apiError.Errors.Count > 0)
                    {
                        error = string.Join(Environment.NewLine,
                            apiError.Errors.SelectMany(x => x.Value));
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }

                Snackbar.AddIrisNotification(error, Severity.Error);
            }
            else
            {
                Snackbar.AddIrisNotification(successMessage, Severity.Success);
            }
        }

        public void HandleResult<T>(Result<T> result, Func<T, string>? success, Func<T, string>? failure)
        {
            HandleResult(result, success?.Invoke(result.Value) ?? "Success", failure?.Invoke(result.Value) ?? "Error");
        }
    }
}

