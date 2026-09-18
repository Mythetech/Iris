using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace Iris.Components.Test
{
    /// <summary>
    /// Iris BUnit test context to make common component initialization easier for test setup
    /// </summary>
    public class IrisTestContext : BunitContext
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
        ///
        /// SetResult is not optional. An explicit Setup takes precedence over Loose mode,
        /// so a handler without a result matches the call and then hangs rather than
        /// returning a default, which surfaced as a five second timeout in whichever test
        /// happened to await MudPopoverProvider.OnAfterRenderAsync under load.
        /// </summary>
        protected IRenderedComponent<MudPopoverProvider> AddPopoverProvider()
        {
            JSInterop.Setup<int>("mudpopoverHelper.countProviders", _ => true).SetResult(1);
            return Render<MudPopoverProvider>();
        }
    }
}

