using Testcontainers.Azurite;

namespace Iris.Integration.Tests.Fixtures;

public class AzuriteContainerFixture : IAsyncLifetime
{
    public AzuriteContainer Container { get; } = new AzuriteBuilder()
        .WithImage("mcr.microsoft.com/azure-storage/azurite")
        .WithCommand("--skipApiVersionCheck")
        .Build();

    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Container.StartAsync();
        ConnectionString = Container.GetConnectionString();
    }

    public Task DisposeAsync() => Container.DisposeAsync().AsTask();
}

[CollectionDefinition("Azurite")]
public class AzuriteCollection : ICollectionFixture<AzuriteContainerFixture>;
