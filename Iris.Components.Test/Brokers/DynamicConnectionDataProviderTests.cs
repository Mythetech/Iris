using System;
using Bunit;
using FluentAssertions;
using Iris.Components.Brokers;
using Iris.Components.Infrastructure;
using Iris.Contracts.Brokers.Models;
using Iris.Contracts.Brokers.Models.Amazon;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;

namespace Iris.Components.Test.Brokers
{
    public class DynamicConnectionDataProviderTests : IrisTestContext
    {
        public DynamicConnectionDataProviderTests()
        {
            // Azure Service Bus is deliberately absent: an unregistered provider falls back
            // to the plain connection string field.
            Services.AddSingleton(new ComponentRegistry<IConnectionDataProvider>()
                .Register<RabbitMqConnectionData>("RabbitMq")
                .Register<AmazonConnectionData>("Amazon"));

            var catalog = Substitute.For<IAwsProfileCatalog>();
            catalog.GetProfiles().Returns(new AwsProfileListing([new AwsProfile("dev", "us-east-1")], Readable: true));
            Services.AddSingleton(catalog);

            AddPopoverProvider();
        }

        private IRenderedComponent<DynamicConnectionDataProvider> RenderFor(string provider)
            => Render<DynamicConnectionDataProvider>(parameters => parameters
                .Add(p => p.Provider, new SupportedProvider { Name = provider }));

        private static Task SwitchToAsync(IRenderedComponent<DynamicConnectionDataProvider> cut, string provider)
            => cut.InvokeAsync(() => cut.Render(parameters => parameters
                .Add(p => p.Provider, new SupportedProvider { Name = provider })));

        private static async Task ChooseAwsProfileAsync(IRenderedComponent<DynamicConnectionDataProvider> cut)
        {
            foreach (var (label, value) in new[] { ("Authentication", AwsAuthModes.Profile), ("Profile", "dev") })
            {
                var select = cut.FindComponents<MudSelect<string>>().Single(s => s.Instance.Label == label);
                await cut.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(value));
            }
        }

        [Fact(DisplayName = "What was chosen for one provider does not ride along to the next")]
        public async Task Switching_provider_starts_from_fresh_data()
        {
            var cut = RenderFor("Amazon");
            await ChooseAwsProfileAsync(cut);

            await SwitchToAsync(cut, "RabbitMq");

            var data = cut.Instance.GetData();
            data.AuthMode.Should().BeNull();
            data.Profile.Should().BeNull();
            data.Region.Should().BeNull();
        }

        [Fact(DisplayName = "Data already handed out is not changed when the form is switched away")]
        public async Task Handed_out_data_survives_a_provider_switch()
        {
            // A connect call that is still running holds this object, and the connection is
            // saved from it when the broker answers.
            var cut = RenderFor("Amazon");
            await ChooseAwsProfileAsync(cut);
            var handedOut = cut.Instance.GetData();

            await SwitchToAsync(cut, "RabbitMq");

            handedOut.AuthMode.Should().Be(AwsAuthModes.Profile);
            handedOut.Profile.Should().Be("dev");
            handedOut.Region.Should().Be("us-east-1");
        }

        [Fact(DisplayName = "Re-rendering for the same provider keeps what was chosen")]
        public async Task The_same_provider_keeps_its_data()
        {
            // The dialog re-renders this component whenever its own state changes, with a
            // provider of the same name. That must not count as a switch.
            var cut = RenderFor("Amazon");
            await ChooseAwsProfileAsync(cut);

            await SwitchToAsync(cut, "Amazon");

            cut.Instance.GetData().Profile.Should().Be("dev");
        }

        [Fact(DisplayName = "Dynamic connection data component provider can render rabbitmq")]
        public void DynamicProvider_CanRender_RabbitMq()
        {
            // Arrange 
            var provider = new SupportedProvider { Name = "RabbitMq" };
            var cut = Render<DynamicConnectionDataProvider>(parameters => parameters
                .Add(p => p.Provider, provider));

            // Act
            var rabbit = cut.FindComponent<RabbitMqConnectionData>();

            // Assert
            rabbit.Should().NotBeNull();
        }

        [Fact(DisplayName = "Dynamic connection data component provider can render azure (default)")]
        public void DynamicProvider_CanRender_Azure()
        {
            // Arrange
            var provider = new SupportedProvider { Name = "Azure Service Bus" };
            var cut = Render<DynamicConnectionDataProvider>(parameters => parameters
                .Add(p => p.Provider, provider));

            // Act
            var connectionStringLabel = cut.Find("input[type='password']");

            // Assert
            connectionStringLabel.TextContent.Should().NotBeNull();
        }

        [Fact(DisplayName = "Dynamic connection data component provider can render amazon sqs")]
        public void DynamicProvider_CanRender_AmazonSqs()
        {
            // Arrange 
            var provider = new SupportedProvider { Name = "Amazon" };
            var cut = Render<DynamicConnectionDataProvider>(parameters => parameters
                .Add(p => p.Provider, provider));

            // Act
            var aws = cut.FindComponent<AmazonConnectionData>();

            // Assert
            aws.Should().NotBeNull();
        }

        [Fact(DisplayName = "Dynamic connection data component provider can render default with unsupported provider")]
        public void DynamicProvider_CanRender_Default()
        {
            // Arrange
            var provider = new SupportedProvider { Name = "FakeProvider" };
            var cut = Render<DynamicConnectionDataProvider>(parameters => parameters
                .Add(p => p.Provider, provider));

            // Act
            var connectionStringLabel = cut.Find("input[type='password']");

            // Assert
            connectionStringLabel.Should().NotBeNull();
        }

        [Fact(DisplayName = "Dynamic connection data component provider can edit default")]
        public async Task DynamicProvider_CanEdit_Default()
        {
            // Arrange
            var provider = new SupportedProvider { Name = "FakeProvider" };
            var cut = Render<DynamicConnectionDataProvider>(parameters => parameters
                .Add(p => p.Provider, provider));

            // Act
            await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "New Value" });

            // Assert
            var connectionData = cut.Instance.GetData();
            connectionData.ConnectionString.Should().Be("New Value");
        }

        [Fact(DisplayName = "Switching from a registered provider to an unregistered one does not return the previous payload")]
        public async Task DynamicProvider_SwitchingToUnregistered_DoesNotReturnStalePayload()
        {
            // Arrange: render the RabbitMq-specific form first so the DynamicComponent @ref is set.
            var cut = Render<DynamicConnectionDataProvider>(parameters => parameters
                .Add(p => p.Provider, new SupportedProvider { Name = "RabbitMq" }));
            cut.FindComponent<RabbitMqConnectionData>().Should().NotBeNull();

            // Act: move to a provider with no registered view, then type a connection string.
            await cut.InvokeAsync(() => cut.Render(parameters => parameters
                .Add(p => p.Provider, new SupportedProvider { Name = "FakeProvider" })));
            await cut.Find("input[type='password']").InputAsync(new ChangeEventArgs { Value = "amqp://typed" });

            // Assert: the payload comes from the text field, not the stale RabbitMq component.
            cut.Instance.GetData().ConnectionString.Should().Be("amqp://typed");
        }
    }
}

