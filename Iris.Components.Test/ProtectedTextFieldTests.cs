using Bunit;
using FluentAssertions;
using Iris.Components.Shared;
using Microsoft.AspNetCore.Components;
using MudBlazor.Services;

namespace Iris.Components.Test
{
    public class ProtectedTextFieldTests : TestContext
    {
        public ProtectedTextFieldTests()
        {
            Services.AddMudServices();
        }

        [Fact(DisplayName = "ProtectedTextField can render and bind value")]
        public void ProtectedTextField_CanRenderAndBindValue()
        {
            // Arrange
            var value = "Test Value";
            var cut = RenderComponent<ProtectedTextField>(parameters => parameters
                .Add(p => p.Value, value)
                .Add(p => p.ValueChanged, EventCallback.Factory.Create(this, (string val) => value = val)));

            // Act
            var input = cut.Find("input");
            input.Change("New Value");

            // Assert
            value.Should().Be("New Value");
        }

        [Fact(DisplayName = "Password is hidden by default")]
        public void Password_IsHidden_ByDefault()
        {
            // Arrange
            var cut = RenderComponent<ProtectedTextField>(parameters => parameters
                .Add(p => p.Value, "Test Password"));

            // Assert
            var input = cut.Find("input");
            input.GetAttribute("type").Should().Be("password");
        }

        [Fact(DisplayName = "Clicking visibility toggle shows password")]
        public void Clicking_VisibilityToggle_ShowsPassword()
        {
            // Arrange
            var cut = RenderComponent<ProtectedTextField>(parameters => parameters
                .Add(p => p.Value, "Test Password"));

            // Act
            cut.Find("button").Click();

            // Assert
            var input = cut.Find("input");
            input.GetAttribute("type").Should().Be("text");
        }

        [Fact(DisplayName = "Clicking visibility toggle twice hides password")]
        public void Clicking_VisibilityToggle_Twice_HidesPassword()
        {
            // Arrange
            var cut = RenderComponent<ProtectedTextField>(parameters => parameters
                .Add(p => p.Value, "Test Password"));

            // Act
            cut.Find("button").Click();
            cut.Find("button").Click();

            // Assert
            var input = cut.Find("input");
            input.GetAttribute("type").Should().Be("password");
        }
    }
}

