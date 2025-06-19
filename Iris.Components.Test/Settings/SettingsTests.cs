using Bunit;
using FluentAssertions;
using Iris.Components.Messaging;
using Iris.Components.Settings;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace Iris.Components.Test.Settings;

public class SettingsTests : IrisTestContext
{
    private IMessagingLayoutService Layout { get; set; }
    
    public SettingsTests()
    {
        Services.AddSingleton<ISectionAggregator, SectionAggregator>();
        Layout = Substitute.For<IMessagingLayoutService>();
        Services.AddSingleton<IMessagingLayoutService>(Layout);
        Services.AddSingleton<LayoutState>();
    }

    [Fact(DisplayName = "Can render settings page")]
    public void Can_Render_SettingsPage()
    {
        // Act
        var cut = RenderComponent<Components.Settings.Settings>();
        
        // Assert
        cut.Should().NotBeNull();
    }
    
    [Fact(DisplayName = "Can render settings sections")]
    public void Can_Render_SettingsSections()
    {
        // Arrange
        Services.AddSettingsProviders(typeof(SimpleSection).Assembly);
        Services.AddSingleton<MessageState>();
        
        // Act
        var cut = RenderComponent<Components.Settings.Settings>();
        
        cut.FindComponents<Section>().Count.Should().BeGreaterThan(0);
        
        // Assert
        cut.Should().NotBeNull();
    }
}