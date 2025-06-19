using System;
using Bunit;
using FluentAssertions;
using Iris.Components.Brokers;
using Iris.Contracts.Brokers.Models;
using Iris.Contracts.Brokers.Models.Amazon;
using MudBlazor;
using MudBlazor.Services;

namespace Iris.Components.Test.Brokers
{
    public class AmazonConnectionDataTests : TestContext
    {
        public AmazonConnectionDataTests()
        {
            Services.AddMudServices();
            JSInterop.SetupVoid("mudPopover.initialize", _ => true);
            JSInterop.Setup<int>("mudpopoverHelper.countProviders", _ => true);
            RenderComponent<MudPopoverProvider>();
        }

        [Fact(DisplayName = "Can render AmazonConnectionData")]
        public void Can_Render_AmazonConnectionData()
        {
            // Arrange
            var connectionData = new ConnectionData();

            // Act
            var cut = RenderComponent<AmazonConnectionData>(parameters => parameters
                .Add(p => p.Data, connectionData));

            // Assert
            cut.Instance.Should().NotBeNull();
            cut.Markup.Should().Contain("Region");
            cut.Markup.Should().Contain("Access Key");
            cut.Markup.Should().Contain("Secret Access Key");
        }

        [Fact(DisplayName = "RegionSearch returns correct regions")]
        public async Task RegionSearch_Returns_Correct_Regions()
        {
            // Arrange
            var connectionData = new ConnectionData();
            var cut = RenderComponent<AmazonConnectionData>(parameters => parameters
                .Add(p => p.Data, connectionData));

            // Act
            var regions = await cut.Instance.RegionSearch("us", CancellationToken.None);

            // Assert
            regions.Should().NotBeNull();
            regions.Should().NotBeEmpty();
            regions.All(r => r.DisplayName.Contains("us", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
        }

        [Fact(DisplayName = "Selecting a region updates ConnectionData.Region")]
        public async Task Selecting_Region_Updates_ConnectionData_Region()
        {
            // Arrange
            var connectionData = new ConnectionData();
            var cut = RenderComponent<AmazonConnectionData>(parameters => parameters
                .Add(p => p.Data, connectionData));

            // Act
            var autocomplete = cut.FindComponent<MudAutocomplete<RegionEndpoint>>();

            await autocomplete.InvokeAsync(() => autocomplete.Instance.SelectOptionAsync(RegionEndpoint.USEast1));

            // Assert
            connectionData.Region.Should().Be(RegionEndpoint.USEast1.SystemName);
        }
    }
}

