using FluentAssertions;
using Iris.Brokers;
using Iris.Brokers.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Iris.Integration.Tests.Brokers;

public class BrokerConnectionManagerTests
{
    private readonly IBrokerConnectionManager _manager;

    public BrokerConnectionManagerTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IBrokerConnectionManager, BrokerConnectionManager>();
        services.AddSingleton<IConnector, Iris.Brokers.RabbitMQ.RabbitMqConnector>();
        services.AddSingleton<IConnector, Iris.Brokers.Azure.AzureConnector>();
        services.AddSingleton<IConnector, Iris.Brokers.Amazon.AmazonWebServicesConnector>();
        services.AddLogging();

        var provider = services.BuildServiceProvider();
        _manager = provider.GetRequiredService<IBrokerConnectionManager>();
    }

    [Fact(DisplayName = "Broker manager can retrieve azure connector")]
    public void GetAzure_Returns_AzureConnector()
    {
        var azure = _manager.GetAzure();

        azure.Should().NotBeNull();
        azure.Should().BeAssignableTo<IConnector>();
    }

    [Fact(DisplayName = "Broker manager can retrieve aws connector")]
    public void GetAws_Returns_AwsConnector()
    {
        var aws = _manager.GetAws();

        aws.Should().NotBeNull();
        aws.Should().BeAssignableTo<IConnector>();
    }

    [Fact(DisplayName = "Broker manager can retrieve rabbitmq connector")]
    public void GetRabbitMq_Returns_RabbitMqConnector()
    {
        var rabbit = _manager.GetRabbitMq();

        rabbit.Should().NotBeNull();
        rabbit.Should().BeAssignableTo<IConnector>();
    }
}
