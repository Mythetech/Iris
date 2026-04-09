using FastEndpoints.Testing;
using FluentAssertions;
using Iris.Brokers;
using Iris.Brokers.Extensions;
using Iris.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Iris.Integration.Tests.Brokers;

public class BrokerConnectionManagerTests : IClassFixture<IrisWebApplicationFactory>
{
    private readonly IrisWebApplicationFactory _fixture;

    public BrokerConnectionManagerTests(IrisWebApplicationFactory fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Broker manager can retrieve azure connector")]
    public void GetAzure_Returns_AzureConnector()
    {
        // Arrange
        using var scope = _fixture.Services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IBrokerConnectionManager>();

        // Act
        var azure = manager.GetAzure();

        // Assert
        azure.Should().NotBeNull();
        azure.Should().BeAssignableTo<IConnector>();
    }

    [Fact(DisplayName = "Broker manager can retrieve aws connector")]
    public void GetAws_Returns_AwsConnector()
    {
        // Arrange
        using var scope = _fixture.Services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IBrokerConnectionManager>();

        // Act
        var aws = manager.GetAws();

        // Assert
        aws.Should().NotBeNull();
        aws.Should().BeAssignableTo<IConnector>();
    }
    
    [Fact(DisplayName = "Broker manager can retrieve rabbitmq connector")]
    public void GetAws_Returns_RabbitMqConnector()
    {
        // Arrange
        using var scope = _fixture.Services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IBrokerConnectionManager>();

        // Act
        var rabbit = manager.GetRabbitMq();

        // Assert
        rabbit.Should().NotBeNull();
        rabbit.Should().BeAssignableTo<IConnector>();
    }
}