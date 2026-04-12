using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Iris.Integration.Tests.Fixtures;

public class ElasticMqContainerFixture : IAsyncLifetime
{
    public const ushort SqsPort = 9324;

    public IContainer Container { get; } = new ContainerBuilder()
        .WithImage("softwaremill/elasticmq-native:latest")
        .WithPortBinding(SqsPort, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(SqsPort))
        .Build();

    public Task InitializeAsync() => Container.StartAsync();

    public Task DisposeAsync() => Container.DisposeAsync().AsTask();
}

[CollectionDefinition("ElasticMQ")]
public class ElasticMqCollection : ICollectionFixture<ElasticMqContainerFixture>;
