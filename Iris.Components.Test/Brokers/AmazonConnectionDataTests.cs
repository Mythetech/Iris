using Bunit;
using FluentAssertions;
using Iris.Components.Brokers;
using Iris.Contracts.Brokers.Models;
using Iris.Contracts.Brokers.Models.Amazon;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;

namespace Iris.Components.Test.Brokers
{
    public class AmazonConnectionDataTests : IrisTestContext
    {
        private readonly IAwsProfileCatalog _catalog = Substitute.For<IAwsProfileCatalog>();

        public AmazonConnectionDataTests()
        {
            HasProfiles(
                new AwsProfile("dev", "us-east-1"),
                new AwsProfile("paris", "eu-west-3"),
                new AwsProfile("plain", null));

            // Registered before the popover provider renders: the first render locks the
            // service collection.
            Services.AddSingleton(_catalog);
            AddPopoverProvider();
        }

        private void HasProfiles(params AwsProfile[] profiles)
            => _catalog.GetProfiles().Returns(new AwsProfileListing(profiles, Readable: true));

        private IRenderedComponent<AmazonConnectionData> RenderFor(ConnectionData data)
            => Render<AmazonConnectionData>(parameters => parameters.Add(p => p.Data, data));

        private static Task SelectAsync(IRenderedComponent<AmazonConnectionData> cut, string label, string value)
        {
            var select = cut.FindComponents<MudSelect<string>>().Single(s => s.Instance.Label == label);
            return cut.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(value));
        }

        private static bool HasSelect(IRenderedComponent<AmazonConnectionData> cut, string label)
            => cut.FindComponents<MudSelect<string>>().Any(s => s.Instance.Label == label);

        [Fact(DisplayName = "Can render AmazonConnectionData")]
        public void Can_Render_AmazonConnectionData()
        {
            var cut = RenderFor(new ConnectionData());

            cut.Instance.Should().NotBeNull();
            cut.Markup.Should().Contain("Region");
            cut.Markup.Should().Contain("Access Key");
            cut.Markup.Should().Contain("Secret Access Key");
        }

        [Fact(DisplayName = "RegionSearch returns correct regions")]
        public async Task RegionSearch_Returns_Correct_Regions()
        {
            var cut = RenderFor(new ConnectionData());

            var regions = await cut.Instance.RegionSearch("us", CancellationToken.None);

            regions.Should().NotBeNull();
            regions.Should().NotBeEmpty();
            regions.All(r => r.DisplayName.Contains("us", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
        }

        [Fact(DisplayName = "Selecting a region updates ConnectionData.Region")]
        public async Task Selecting_Region_Updates_ConnectionData_Region()
        {
            var connectionData = new ConnectionData();
            var cut = RenderFor(connectionData);

            var autocomplete = cut.FindComponent<MudAutocomplete<RegionEndpoint>>();
            await autocomplete.InvokeAsync(() => autocomplete.Instance.SelectOptionAsync(RegionEndpoint.USEast1));

            connectionData.Region.Should().Be(RegionEndpoint.USEast1.SystemName);
        }

        [Fact(DisplayName = "The form opens in access key mode and records that on the connection")]
        public void Opens_in_access_key_mode()
        {
            var connectionData = new ConnectionData();

            var cut = RenderFor(connectionData);

            connectionData.AuthMode.Should().Be(AwsAuthModes.AccessKeys);
            HasSelect(cut, "Profile").Should().BeFalse();
        }

        [Fact(DisplayName = "Profile mode shows a profile select and no key fields")]
        public async Task Profile_mode_swaps_the_fields()
        {
            var connectionData = new ConnectionData();
            var cut = RenderFor(connectionData);

            await SelectAsync(cut, "Authentication", AwsAuthModes.Profile);

            connectionData.AuthMode.Should().Be(AwsAuthModes.Profile);
            HasSelect(cut, "Profile").Should().BeTrue();
            cut.Markup.Should().NotContain("Secret Access Key");
            cut.Markup.Should().Contain("Leave blank to use the profile");
        }

        [Fact(DisplayName = "Default credentials mode shows neither keys nor a profile select")]
        public async Task Default_mode_shows_only_region()
        {
            var connectionData = new ConnectionData();
            var cut = RenderFor(connectionData);

            await SelectAsync(cut, "Authentication", AwsAuthModes.DefaultChain);

            connectionData.AuthMode.Should().Be(AwsAuthModes.DefaultChain);
            HasSelect(cut, "Profile").Should().BeFalse();
            cut.Markup.Should().NotContain("Secret Access Key");
            cut.Markup.Should().Contain("Uses AWS environment variables, then the default profile");
        }

        [Fact(DisplayName = "Choosing a profile records it and prefills its region")]
        public async Task Choosing_a_profile_prefills_the_region()
        {
            var connectionData = new ConnectionData();
            var cut = RenderFor(connectionData);
            await SelectAsync(cut, "Authentication", AwsAuthModes.Profile);

            await SelectAsync(cut, "Profile", "dev");

            connectionData.Profile.Should().Be("dev");
            connectionData.Region.Should().Be("us-east-1");
        }

        [Theory(DisplayName = "A profile whose region Iris cannot show leaves the region for the profile to supply")]
        [InlineData("paris")]
        [InlineData("plain")]
        public async Task An_unlisted_region_is_left_blank(string profile)
        {
            var connectionData = new ConnectionData();
            var cut = RenderFor(connectionData);
            await SelectAsync(cut, "Authentication", AwsAuthModes.Profile);
            await SelectAsync(cut, "Profile", "dev");

            await SelectAsync(cut, "Profile", profile);

            connectionData.Profile.Should().Be(profile);
            connectionData.Region.Should().BeNull();
        }

        [Fact(DisplayName = "Leaving access key mode drops the keys that were typed")]
        public async Task Leaving_access_keys_clears_them()
        {
            var connectionData = new ConnectionData { Username = "AKIAKEYS", Password = "secret" };
            var cut = RenderFor(connectionData);

            await SelectAsync(cut, "Authentication", AwsAuthModes.Profile);

            connectionData.Username.Should().BeNull();
            connectionData.Password.Should().BeNull();
        }

        [Fact(DisplayName = "Leaving profile mode drops the profile that was chosen")]
        public async Task Leaving_profile_mode_clears_the_profile()
        {
            var connectionData = new ConnectionData();
            var cut = RenderFor(connectionData);
            await SelectAsync(cut, "Authentication", AwsAuthModes.Profile);
            await SelectAsync(cut, "Profile", "dev");

            await SelectAsync(cut, "Authentication", AwsAuthModes.DefaultChain);

            connectionData.Profile.Should().BeNull();
        }

        [Fact(DisplayName = "With no profiles on the machine, profile mode says how to create one")]
        public async Task No_profiles_shows_a_notice()
        {
            HasProfiles();
            var cut = RenderFor(new ConnectionData());

            await SelectAsync(cut, "Authentication", AwsAuthModes.Profile);

            cut.FindComponents<MudAlert>().Should().ContainSingle(alert => alert.Instance.Severity == Severity.Info);
            HasSelect(cut, "Profile").Should().BeFalse();
        }

        [Fact(DisplayName = "When the profile files cannot be read, the form says so and takes a typed name")]
        public async Task Unreadable_profiles_fall_back_to_a_typed_name()
        {
            // One broken entry stops the AWS SDK listing any profile, while the good ones
            // still work by name. "No profiles found" would be untrue and a dead end.
            _catalog.GetProfiles().Returns(AwsProfileListing.Unreadable);
            var connectionData = new ConnectionData();
            var cut = RenderFor(connectionData);
            await SelectAsync(cut, "Authentication", AwsAuthModes.Profile);

            cut.FindComponents<MudAlert>().Should().ContainSingle(alert => alert.Instance.Severity == Severity.Warning);
            HasSelect(cut, "Profile").Should().BeFalse();

            var field = cut.FindComponents<MudTextField<string>>().Single(f => f.Instance.Label == "Profile");
            await cut.InvokeAsync(() => field.Instance.ValueChanged.InvokeAsync(" dev "));

            connectionData.Profile.Should().Be("dev");
        }

        [Fact(DisplayName = "Typing a profile name leaves a chosen region alone")]
        public async Task A_typed_profile_keeps_the_region()
        {
            _catalog.GetProfiles().Returns(AwsProfileListing.Unreadable);
            var connectionData = new ConnectionData();
            var cut = RenderFor(connectionData);
            await SelectAsync(cut, "Authentication", AwsAuthModes.Profile);
            var autocomplete = cut.FindComponent<MudAutocomplete<RegionEndpoint>>();
            await autocomplete.InvokeAsync(() => autocomplete.Instance.SelectOptionAsync(RegionEndpoint.USEast1));

            var field = cut.FindComponents<MudTextField<string>>().Single(f => f.Instance.Label == "Profile");
            await cut.InvokeAsync(() => field.Instance.ValueChanged.InvokeAsync("dev"));

            connectionData.Region.Should().Be("us-east-1");
        }

    }
}
