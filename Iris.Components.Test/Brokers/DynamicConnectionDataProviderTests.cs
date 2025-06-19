using System;
using Bunit;
using FluentAssertions;
using FluentAssertions.Common;
using Iris.Components.Brokers;
using Iris.Components.Shared;
using Iris.Contracts.Brokers.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace Iris.Components.Test.Brokers
{
    public class DynamicConnectionDataProviderTests : TestContext
    {
        public DynamicConnectionDataProviderTests()
        {
            var providerLookup = new Dictionary<string, Type>
        {
            { "azureservicebus", null },
            { "rabbitmq", typeof(RabbitMqConnectionData) },
                {"amazon", typeof(AmazonConnectionData) }
        };
            Services.AddSingleton(providerLookup);
            Services.AddMudServices();
            JSInterop.SetupVoid("mudPopover.initialize", _ => true);
            JSInterop.Setup<int>("mudpopoverHelper.countProviders", _ => true);
            RenderComponent<MudPopoverProvider>();
        }

        [Fact(DisplayName = "Dynamic connection data component provider can render rabbitmq")]
        public void DynamicProvider_CanRender_RabbitMq()
        {
            // Arrange 
            var provider = new SupportedProvider { Name = "RabbitMq" };
            var cut = RenderComponent<DynamicConnectionDataProvider>(parameters => parameters
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
            var cut = RenderComponent<DynamicConnectionDataProvider>(parameters => parameters
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
            var cut = RenderComponent<DynamicConnectionDataProvider>(parameters => parameters
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
            var cut = RenderComponent<DynamicConnectionDataProvider>(parameters => parameters
                .Add(p => p.Provider, provider));

            // Act
            var connectionStringLabel = cut.Find("input[type='password']");

            // Assert
            connectionStringLabel.Should().NotBeNull();
        }

        [Fact(DisplayName = "Dynamic connection data component provider can edit default")]
        public void DynamicProvider_CanEdit_Default()
        {
            // Arrange
            var provider = new SupportedProvider { Name = "FakeProvider" };
            var cut = RenderComponent<DynamicConnectionDataProvider>(parameters => parameters
                .Add(p => p.Provider, provider));
            var connectionData = new ConnectionData
            {
                Provider = provider.Name
            };

            // Act
            var protectedTextField = cut.FindComponent<ProtectedTextField>();
            protectedTextField.SetParametersAndRender(parameters => parameters
                .Add(p => p.Value, "Test Value")
                .Add(p => p.ValueChanged, EventCallback.Factory.Create(this, (string val) => connectionData.ConnectionString = val)));
            protectedTextField.Find("input").Change("New Value");

            // Assert
            connectionData.ConnectionString.Should().Be("New Value");
        }
    }
}

