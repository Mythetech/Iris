using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
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

        /// <summary>
        /// Opt in from a test class whose component renders a MudSelect, MudMenu or
        /// MudTooltip. MudBlazor builds the popover during initialization and throws
        /// without a provider in the tree, and it renders popover content into that
        /// fragment rather than under the component itself.
        /// </summary>
        protected IRenderedComponent<MudPopoverProvider> AddPopoverProvider()
        {
            JSInterop.Setup<int>("mudpopoverHelper.countProviders", _ => true);
            return RenderComponent<MudPopoverProvider>();
        }
    }
}

