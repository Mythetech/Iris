using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Iris.Integration.Tests.Fixtures;

/// <summary>
/// LocalEmu, the open-source AWS emulator TsTransit settled on. It replaced ElasticMQ, which
/// serves SQS only: MassTransit's SQS transport creates an SNS topic per message type and
/// subscribes the endpoint queue to it, so a round-trip test needs both services. One emulator
/// now covers the connector tests and the framework one.
///
/// Pinned by digest, because a floating tag changes emulator behaviour under a commit that
/// changed nothing. The published port is ephemeral: the emulator hands back queue URLs bearing
/// its own internal port, and the AWS SDK ignores that host and uses the configured ServiceURL,
/// which is why a random published port works here.
/// </summary>
public class LocalEmuContainerFixture : IAsyncLifetime
{
    public const ushort EdgePort = 4566;

    public IContainer Container { get; } = new ContainerBuilder()
        .WithImage("localemu/localemu@sha256:86a84bf21e3c0cf2cb695bdd890bce642e8e3d852c41f46d7aa8123551cee10b")
        .WithEnvironment("SERVICES", "sqs,sns")
        // Path-style queue URLs (http://host/<account>/<queue>) need no wildcard DNS.
        .WithEnvironment("SQS_ENDPOINT_STRATEGY", "path")
        .WithPortBinding(EdgePort, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(EdgePort).ForPath("/")))
        .Build();

    public string ServiceUrl => $"http://localhost:{Container.GetMappedPublicPort(EdgePort)}";

    public Task InitializeAsync() => Container.StartAsync();

    public Task DisposeAsync() => Container.DisposeAsync().AsTask();
}

[CollectionDefinition("LocalEmu")]
public class LocalEmuCollection : ICollectionFixture<LocalEmuContainerFixture>;
