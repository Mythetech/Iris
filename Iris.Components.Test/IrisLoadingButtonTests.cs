using Bunit;
using FluentAssertions;
using Iris.Components.Shared;
using Iris.Components.Theme;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Services;
using Xunit;

namespace Iris.Components.Test
{
    public sealed class IrisLoadingButtonTests : IrisTestContext
    {
        private IRenderedComponent<MudPopoverProvider> _popoverProvider;
        
        public IrisLoadingButtonTests()
        {
            _popoverProvider = RenderComponent<MudPopoverProvider>();
        }

        [Fact(DisplayName = "Button renders with correct text")]
        public void Button_Renders_With_Correct_Text()
        {
            // Arrange
            var buttonText = "Click Me";
            var cut = RenderComponent<IrisLoadingButton>(parameters => parameters
                .Add(p => p.Text, buttonText));

            // Act
            var button = cut.Find("button");

            // Assert
            button.TextContent.Should().Contain(buttonText);
        }

        [Fact(DisplayName = "Button shows loading text when loading")]
        public void Button_Shows_LoadingText_When_Loading()
        {
            // Arrange
            var loadingText = "Loading...";
            var cut = RenderComponent<IrisLoadingButton>(parameters => parameters
                .Add(p => p.Loading, true)
                .Add(p => p.LoadingText, loadingText));

            // Act
            var button = cut.Find("button");

            // Assert
            button.TextContent.Should().Contain(loadingText);
        }

        [Fact(DisplayName = "Button is disabled when loading")]
        public void Button_Is_Disabled_When_Loading()
        {
            // Arrange
            var cut = RenderComponent<IrisLoadingButton>(parameters => parameters
                .Add(p => p.Loading, true));

            // Act
            var button = cut.Find("button");

            // Assert
            button.HasAttribute("disabled").Should().BeTrue();
        }

        [Fact(DisplayName = "Click event triggers correctly")]
        public void Click_Event_Triggers_Correctly()
        {
            // Arrange
            var clicked = false;
            var cut = RenderComponent<IrisLoadingButton>(parameters => parameters
                .Add(p => p.OnClick, EventCallback.Factory.Create(this, () => clicked = true)));

            // Act
            cut.Find("button").Click();

            // Assert
            clicked.Should().BeTrue();
        }

        [Fact(DisplayName = "Icon is displayed when set")]
        public void Icon_Is_Displayed_When_Set()
        {
            // Arrange
            var cut = RenderComponent<IrisLoadingButton>(parameters => parameters
                .Add(p => p.Icon, IrisIcons.Home));

            // Act & Assert - Material Symbols icons render as <span class="material-symbols-rounded">icon_name</span>
            var icon = cut.Find(".material-symbols-rounded");
            icon.Should().NotBeNull();
            icon.TextContent.Should().Contain("house");
        }
    }
}