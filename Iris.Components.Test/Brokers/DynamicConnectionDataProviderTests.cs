using System;
using Bunit;
using FluentAssertions;
using Iris.Components.Brokers;
using Iris.Components.Infrastructure;
using Iris.Contracts.Brokers.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

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
            AddPopoverProvider();
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

