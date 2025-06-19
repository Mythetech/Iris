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
            JSInterop.SetupVoid("mudPopover.initialize", _ => true);
            JSInterop.SetupVoid("mudPopover.connect", _ => true);
            JSInterop.Setup<int>("mudpopoverHelper.countProviders", _ => true);
            JSInterop.SetupVoid("mudKeyInterceptor.connect", _ => true);
        }
    }
}

