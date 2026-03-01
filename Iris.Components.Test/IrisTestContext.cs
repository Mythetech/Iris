using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Iris.Components.Test
{
    /// <summary>
    /// Iris BUnit test context to make common component initialization easier for test setup
    /// </summary>
    public class IrisTestContext : TestContext
    {
        public IrisTestContext()
        {
            Services.AddMudServices();
            JSInterop.Mode = JSRuntimeMode.Loose;
        }
    }
}

