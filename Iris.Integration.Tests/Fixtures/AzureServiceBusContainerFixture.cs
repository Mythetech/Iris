using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace Iris.Integration.Tests.Fixtures;

/// <summary>
/// Runs the Service Bus suite against OpenServiceBus, the same emulator TsTransit uses.
///
/// It replaced Microsoft's emulator image, which needed a SQL Edge sidecar on its own Docker
/// network. SQL Edge is amd64 only, so on Apple Silicon it ran under emulation and took minutes
/// to come up, and the whole arrangement had to bind host port 5672 because the connection
/// string named no other one. Anything already holding 5672, which locally is a RabbitMQ
/// container that stays up, made the suite unrunnable outside CI.
///
/// OpenServiceBus is one container, it speaks the Service Bus AMQP data plane the connector
/// uses, and it accepts a port in the endpoint, so both ports are ephemeral and nothing
/// collides. Its management plane is a small JSON REST API rather than the real Service Bus
/// management protocol, so entities are seeded through <see cref="CreateQueue"/> below instead
/// of a ServiceBusAdministrationClient. The image is pinned by digest because a floating tag
/// changes emulator behaviour under a commit that changed nothing.
/// </summary>
public class AzureServiceBusContainerFixture : IAsyncLifetime
{
    public const string MainQueue = "iris-main-test";
    public const string DlqQueue = "iris-dlq-test";

    private const string Image =
        "mauritsarissen/openservicebus@sha256:c944c328793d26f684d4dc0d097dd10b4aff002a189a03abd8f86b11b4ce9e0c";
    private const string AmqpPort = "5672/tcp";
    private const string ManagementPort = "5300/tcp";

    private readonly DockerClient _docker = new DockerClientConfiguration().CreateClient();
    private readonly HttpClient _http = new();
    private string? _containerId;

    /// <summary>
    /// Set once the emulator is up. The port is whatever Docker assigned, which is why this is
    /// an instance member rather than the constant it used to be.
    /// </summary>
    public string ConnectionString { get; private set; } = string.Empty;

    public string ManagementUrl { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        await PullImageIfMissing(Image, cts.Token);

        var created = await _docker.Containers.CreateContainerAsync(
            new CreateContainerParameters
            {
                Image = Image,
                ExposedPorts = new Dictionary<string, EmptyStruct>
                {
                    [AmqpPort] = default,
                    [ManagementPort] = default,
                },
                HostConfig = new HostConfig
                {
                    // An empty HostPort asks Docker for a free one, so two runs, or a run
                    // alongside another broker, never contend for the same number.
                    PortBindings = new Dictionary<string, IList<PortBinding>>
                    {
                        [AmqpPort] = [new PortBinding { HostPort = string.Empty }],
                        [ManagementPort] = [new PortBinding { HostPort = string.Empty }],
                    },
                    AutoRemove = true,
                },
            },
            cts.Token);
        _containerId = created.ID;

        await _docker.Containers.StartContainerAsync(_containerId, null, cts.Token);

        var inspection = await _docker.Containers.InspectContainerAsync(_containerId, cts.Token);
        ConnectionString =
            $"Endpoint=sb://localhost:{HostPortOf(inspection, AmqpPort)};" +
            "SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;" +
            "UseDevelopmentEmulator=true;";
        ManagementUrl = $"http://localhost:{HostPortOf(inspection, ManagementPort)}";

        await WaitForHealthy(cts.Token);

        // The queues the suite reads and writes. MaxDeliveryCount of 1 on the dead-letter
        // queue is what lets a single abandon move a message to its dead-letter sub-queue.
        await CreateQueue(MainQueue, maxDeliveryCount: 10, lockDuration: "00:01:00", cts.Token);
        await CreateQueue(DlqQueue, maxDeliveryCount: 1, lockDuration: "00:05:00", cts.Token);
    }

    public async Task DisposeAsync()
    {
        if (_containerId is not null)
        {
            try
            {
                // AutoRemove takes the container away once it stops.
                await _docker.Containers.StopContainerAsync(_containerId,
                    new ContainerStopParameters { WaitBeforeKillSeconds = 5 });
            }
            catch (DockerApiException)
            {
                // Best effort. A container that is already gone is the outcome we wanted.
            }
        }

        _http.Dispose();
        _docker.Dispose();
    }

    private static string HostPortOf(ContainerInspectResponse inspection, string containerPort)
    {
        if (inspection.NetworkSettings.Ports.TryGetValue(containerPort, out var bindings)
            && bindings is { Count: > 0 })
        {
            return bindings[0].HostPort;
        }

        throw new InvalidOperationException(
            $"The emulator container published no host port for {containerPort}.");
    }

    private async Task WaitForHealthy(CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        var lastError = "no attempts made";

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var response = await _http.GetAsync($"{ManagementUrl}/health", ct);
                if (response.IsSuccessStatusCode)
                    return;

                lastError = $"HTTP {(int)response.StatusCode}";
            }
            catch (HttpRequestException ex)
            {
                lastError = ex.Message;
            }

            await Task.Delay(500, ct);
        }

        throw new TimeoutException(
            $"OpenServiceBus emulator was not healthy within 60s. Last status: {lastError}");
    }

    /// <summary>
    /// Seeds one queue through the emulator's REST API. Durations are .NET TimeSpan text, not
    /// ISO-8601 durations; sending "PT1M" is rejected with a 400.
    /// </summary>
    private async Task CreateQueue(string name, int maxDeliveryCount, string lockDuration, CancellationToken ct)
    {
        var response = await _http.PutAsJsonAsync(
            $"{ManagementUrl}/queues/{Uri.EscapeDataString(name)}",
            new { maxDeliveryCount, lockDuration },
            ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Creating queue '{name}' returned {(int)response.StatusCode}: " +
                await response.Content.ReadAsStringAsync(ct));
        }
    }

    private async Task PullImageIfMissing(string image, CancellationToken ct)
    {
        try
        {
            await _docker.Images.InspectImageAsync(image, ct);
        }
        catch (DockerImageNotFoundException)
        {
            var separator = image.IndexOf('@');
            await _docker.Images.CreateImageAsync(
                new ImagesCreateParameters
                {
                    FromImage = image[..separator],
                    Tag = image[(separator + 1)..],
                },
                null,
                new Progress<JSONMessage>(),
                ct);
        }
    }
}

[CollectionDefinition("AzureServiceBus")]
public class AzureServiceBusCollection : ICollectionFixture<AzureServiceBusContainerFixture>;
