using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Iris.Integration.Tests.Fixtures;

/// <summary>
/// Google's own Pub/Sub emulator, the one Wolverine's Pub/Sub transport is tested against.
///
/// Pinned by digest, for the reason every other fixture here is: a floating tag changes
/// emulator behaviour under a commit that changed nothing. The published port is ephemeral, so
/// it cannot collide with an emulator a developer already has on 8085.
///
/// The emulator has no authentication and no IAM. These tests therefore say nothing about
/// credentials; that path is checked by hand against a real project before a release.
/// </summary>
public class PubSubEmulatorFixture : IAsyncLifetime
{
    public const ushort EmulatorPort = 8085;

    public IContainer Container { get; } = new ContainerBuilder()
        .WithImage("gcr.io/google.com/cloudsdktool/google-cloud-cli@sha256:8d8573471c489a409f03891d2c24d177fa531276184f7002b9a2d853d1b3ed54")
        .WithCommand("gcloud", "beta", "emulators", "pubsub", "start", "--host-port=0.0.0.0:8085")
        .WithPortBinding(EmulatorPort, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(EmulatorPort).ForPath("/")))
        .Build();

    public string Host => $"localhost:{Container.GetMappedPublicPort(EmulatorPort)}";

    public ValueTask InitializeAsync() => new(Container.StartAsync());

    public ValueTask DisposeAsync() => Container.DisposeAsync();
}

[CollectionDefinition("PubSubEmulator")]
public class PubSubEmulatorCollection : ICollectionFixture<PubSubEmulatorFixture>;
