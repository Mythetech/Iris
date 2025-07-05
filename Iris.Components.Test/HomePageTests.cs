using Bunit;
using FluentAssertions;
using Iris.Components.Home;
using Iris.Components.Brokers;
using Iris.Contracts.Brokers.Models;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;

namespace Iris.Components.Test;

public class HomePageTests : TestContext
{
    private IBrokerService _mockBrokerService;

    public HomePageTests()
    {
        _mockBrokerService = Substitute.For<IBrokerService>();
        Services.AddSingleton(_mockBrokerService);
        
        Services.AddMudServices();
        
        JSInterop.SetupModule("./_content/PSC.Blazor.Components.Chartjs/Chart.js")
            .SetupVoid("chartSetup", _ => true);

        JSInterop.SetupVoid("mudPopover.initialize", _ => true);
        JSInterop.SetupVoid("mudPopover.connect", _ => true);
        JSInterop.SetupVoid("addClickListener", _ => true);
        JSInterop.SetupVoid("mudKeyInterceptor.connect", _ => true);
        JSInterop.Setup<int>("mudpopoverHelper.countProviders", _ => true);
        RenderComponent<MudPopoverProvider>();
    }

    [Fact(DisplayName = "Can render Home Page")]
    public void Can_Render_HomePage()
    {
        // Arrange
        string provider = "FakeProvider";
        string address = "fakeprovider.messagebus.test";

        _mockBrokerService.GetSupportedProvidersAsync().Returns(new List<SupportedProvider>()
        {
           new()
           {
               Name = provider,
           },
        });

        _mockBrokerService.GetProvidersAsync().Returns(new List<Provider>()
        {
            new()
            {
                Name = provider,
                Address = address,
                Endpoints = 2,
            }
        });

        _mockBrokerService.GetEndpointsAsync().Returns(new List<EndpointDetails>()
        {
            new()
            {
                Provider = provider,
                Address = address,
                Name = "endpoint-one",
                Type = "Queue",
            },
            new()
            {
                Provider = provider,
                Address = address,
                Name = "endpoint-two",
                Type = "Topic",
            },
        });

        // Act
        var component = RenderComponent<Home.Home>();

        // Assert
        component.Should().NotBeNull();

        component.FindComponent<ConnectionDoughnut>().Should().NotBeNull();
        component.FindComponent<ProviderCarousel>().Should().NotBeNull();
        component.FindComponent<ProviderEndpointBarChart>().Should().NotBeNull();
        component.FindComponent<ProviderEndpointCard>().Should().NotBeNull();
    }

    [Fact(DisplayName = "Can render Home Page loading states")]
    public void Can_Render_HomePage_Loaders()
    {
        // Arrange
        var spcs = new TaskCompletionSource<List<SupportedProvider>>();
        var pcs = new TaskCompletionSource<List<Provider>>();
        var ecs = new TaskCompletionSource<List<EndpointDetails>>();


        _mockBrokerService.GetSupportedProvidersAsync().Returns(spcs.Task);
        _mockBrokerService.GetProvidersAsync().Returns(pcs.Task);
        _mockBrokerService.GetEndpointsAsync().Returns(ecs.Task);


        // Act
        var component = RenderComponent<Home.Home>();

        // Assert
        component.Should().NotBeNull();

        component.FindComponent<ConnectionDoughnutLoader>().Should().NotBeNull();
        component.FindComponent<ProviderCarouselLoader>().Should().NotBeNull();
        component.FindComponent<ProviderEndpointBarChartLoader>().Should().NotBeNull();
        component.FindComponent<ProviderEndpointCardLoader>().Should().NotBeNull();
    }

    [Fact(DisplayName = "Can filter endpoints by type")]
    public async Task Can_Filter_Endpoints_By_Type()
    {
        // Arrange
        string provider = "FakeProvider";
        string address = "fakeprovider.messagebus.test";

        _mockBrokerService.GetSupportedProvidersAsync().Returns(new List<SupportedProvider>()
    {
        new()
        {
            Name = provider,
        },
    });

        _mockBrokerService.GetProvidersAsync().Returns(new List<Provider>()
    {
        new()
        {
            Name = provider,
            Address = address,
            Endpoints = 2,
        }
    });

        _mockBrokerService.GetEndpointsAsync().Returns(new List<EndpointDetails>()
    {
        new()
        {
            Provider = provider,
            Address = address,
            Name = "endpoint-one",
            Type = "Queue",
        },
        new()
        {
            Provider = provider,
            Address = address,
            Name = "endpoint-two",
            Type = "Topic",
        },
    });

        var component = RenderComponent<Home.Home>();
        var original = component.FindComponent<ProviderEndpointCard>();
        original.FindAll(".mud-table-row").Count.Should().Be(4);

        // Act
        var endpointBarChart = component.FindComponent<ProviderEndpointBarChart>();
        var chip = component.FindComponent<IrisChip>();

        await component.InvokeAsync(chip.Instance.ClickAsync);

        // Assert
        var card = component.FindComponent<ProviderEndpointCard>();
        card.FindAll(".mud-table-row").Count.Should().Be(3);
    }
}
