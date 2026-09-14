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

    public ValueTask InitializeAsync() => new(Container.StartAsync());

    public ValueTask DisposeAsync() => Container.DisposeAsync();
}

[CollectionDefinition("RabbitMQ")]
public class RabbitMqCollection : ICollectionFixture<RabbitMqContainerFixture>;
