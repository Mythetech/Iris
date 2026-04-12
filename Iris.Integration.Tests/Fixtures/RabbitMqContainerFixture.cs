using Testcontainers.RabbitMq;

namespace Iris.Integration.Tests.Fixtures;

public class RabbitMqContainerFixture : IAsyncLifetime
{
    public RabbitMqContainer Container { get; } = new RabbitMqBuilder()
        .WithImage("rabbitmq:3-management")
        .WithUsername("guest")
        .WithPassword("guest")
        .WithPortBinding(5672, true)
        .WithPortBinding(15672, true)
        .WithExposedPort(15672)
        .Build();

    public Task InitializeAsync() => Container.StartAsync();

    public Task DisposeAsync() => Container.DisposeAsync().AsTask();
}

[CollectionDefinition("RabbitMQ")]
public class RabbitMqCollection : ICollectionFixture<RabbitMqContainerFixture>;
