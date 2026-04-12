using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace Iris.Integration.Tests.Fixtures;

public class AzureServiceBusContainerFixture : IAsyncLifetime
{
    private const string MainQueue = "iris-main-test";
    private const string DlqQueue = "iris-dlq-test";
    private const string SqlPassword = "YourStrongPassword123!";
    private const ushort ServiceBusPort = 5672;

    public const string ConnectionString =
        "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;" +
        "SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

    private readonly DockerClient _docker = new DockerClientConfiguration().CreateClient();
    private string? _networkId;
    private string? _sqlContainerId;
    private string? _sbContainerId;

    public async Task InitializeAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        // 1. Create a dedicated network
        var network = await _docker.Networks.CreateNetworkAsync(
            new NetworksCreateParameters { Name = $"sb-net-{Guid.NewGuid():N}" },
            cts.Token);
        _networkId = network.ID;

        // 2. Start SQL Edge sidecar
        await PullImageIfMissing("mcr.microsoft.com/azure-sql-edge:latest", cts.Token);
        var sqlCreate = await _docker.Containers.CreateContainerAsync(
            new CreateContainerParameters
            {
                Image = "mcr.microsoft.com/azure-sql-edge:latest",
                Env = ["ACCEPT_EULA=Y", $"MSSQL_SA_PASSWORD={SqlPassword}"],
                HostConfig = new HostConfig
                {
                    NetworkMode = _networkId,
                },
                NetworkingConfig = new NetworkingConfig
                {
                    EndpointsConfig = new Dictionary<string, EndpointSettings>
                    {
                        [_networkId] = new() { Aliases = ["sqledge"] }
                    }
                }
            },
            cts.Token);
        _sqlContainerId = sqlCreate.ID;
        await _docker.Containers.StartContainerAsync(_sqlContainerId, null, cts.Token);

        // 3. Resolve Config.json from build output
        var configPath = Path.Combine(AppContext.BaseDirectory, "Brokers", "Resources", "Config.json");
        if (!File.Exists(configPath))
            throw new FileNotFoundException("Config.json not found in build output", configPath);

        // 4. Start SB emulator with bind-mounted config
        await PullImageIfMissing("mcr.microsoft.com/azure-messaging/servicebus-emulator:latest", cts.Token);
        var sbCreate = await _docker.Containers.CreateContainerAsync(
            new CreateContainerParameters
            {
                Image = "mcr.microsoft.com/azure-messaging/servicebus-emulator:latest",
                Env =
                [
                    "ACCEPT_EULA=Y",
                    "SQL_SERVER=sqledge",
                    $"MSSQL_SA_PASSWORD={SqlPassword}",
                    "SQL_WAIT_INTERVAL=30",
                ],
                HostConfig = new HostConfig
                {
                    NetworkMode = _networkId,
                    PortBindings = new Dictionary<string, IList<PortBinding>>
                    {
                        [$"{ServiceBusPort}/tcp"] = [new PortBinding { HostPort = ServiceBusPort.ToString() }]
                    },
                    Binds = [$"{configPath}:/ServiceBus_Emulator/ConfigFiles/Config.json:ro"],
                },
                ExposedPorts = new Dictionary<string, EmptyStruct>
                {
                    [$"{ServiceBusPort}/tcp"] = default
                },
                NetworkingConfig = new NetworkingConfig
                {
                    EndpointsConfig = new Dictionary<string, EndpointSettings>
                    {
                        [_networkId] = new() { Aliases = ["servicebus"] }
                    }
                }
            },
            cts.Token);
        _sbContainerId = sbCreate.ID;
        await _docker.Containers.StartContainerAsync(_sbContainerId, null, cts.Token);

        // 5. Poll container logs for the "Successfully Up" message
        await WaitForLogMessage(_sbContainerId, "Emulator Service is Successfully Up",
            TimeSpan.FromMinutes(2), cts.Token);

        // 6. Poll via AMQP peek until pre-declared queues accept connections.
        //    ServiceBusAdministrationClient uses HTTP (port 5300) which the
        //    emulator exposes separately; peeking over AMQP on 5672 is simpler.
        await using var probeClient = new ServiceBusClient(ConnectionString);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        string lastError = "no attempts made";
        while (DateTime.UtcNow < deadline)
        {
            cts.Token.ThrowIfCancellationRequested();
            try
            {
                await using var mainReceiver = probeClient.CreateReceiver(MainQueue);
                await using var dlqReceiver = probeClient.CreateReceiver(DlqQueue);
                // PeekMessageAsync succeeds (even with null result) once the queue exists.
                await mainReceiver.PeekMessageAsync(cancellationToken: cts.Token);
                await dlqReceiver.PeekMessageAsync(cancellationToken: cts.Token);
                return;
            }
            catch (Exception ex)
            {
                lastError = $"{ex.GetType().Name}: {ex.Message}";
            }
            await Task.Delay(1000, cts.Token);
        }
        throw new TimeoutException(
            $"Service Bus emulator queues not ready within 60s. Last status: {lastError}");
    }

    public async Task DisposeAsync()
    {
        if (_sbContainerId is not null)
        {
            try
            {
                await _docker.Containers.StopContainerAsync(_sbContainerId,
                    new ContainerStopParameters { WaitBeforeKillSeconds = 5 });
                await _docker.Containers.RemoveContainerAsync(_sbContainerId,
                    new ContainerRemoveParameters { Force = true });
            }
            catch { /* best-effort */ }
        }

        if (_sqlContainerId is not null)
        {
            try
            {
                await _docker.Containers.StopContainerAsync(_sqlContainerId,
                    new ContainerStopParameters { WaitBeforeKillSeconds = 5 });
                await _docker.Containers.RemoveContainerAsync(_sqlContainerId,
                    new ContainerRemoveParameters { Force = true });
            }
            catch { /* best-effort */ }
        }

        if (_networkId is not null)
        {
            try { await _docker.Networks.DeleteNetworkAsync(_networkId); }
            catch { /* best-effort */ }
        }

        _docker.Dispose();
    }

    private async Task WaitForLogMessage(string containerId, string target,
        TimeSpan timeout, CancellationToken ct)
    {
        using var logCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        logCts.CancelAfter(timeout);

        var muxStream = await _docker.Containers.GetContainerLogsAsync(containerId,
            false,
            new ContainerLogsParameters { ShowStdout = true, ShowStderr = true, Follow = true },
            logCts.Token);

        var buffer = new byte[8192];
        while (!logCts.Token.IsCancellationRequested)
        {
            var result = await muxStream.ReadOutputAsync(buffer, 0, buffer.Length, logCts.Token);
            if (result.Count == 0)
            {
                await Task.Delay(250, logCts.Token);
                continue;
            }

            var text = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
            if (text.Contains(target, StringComparison.OrdinalIgnoreCase))
                return;
        }

        throw new TimeoutException(
            $"Container {containerId} did not log \"{target}\" within {timeout.TotalSeconds}s.");
    }

    private async Task PullImageIfMissing(string image, CancellationToken ct)
    {
        try
        {
            await _docker.Images.InspectImageAsync(image, ct);
        }
        catch (DockerImageNotFoundException)
        {
            var parts = image.Split(':');
            await _docker.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = parts[0], Tag = parts.Length > 1 ? parts[1] : "latest" },
                null,
                new Progress<JSONMessage>(),
                ct);
        }
    }
}

[CollectionDefinition("AzureServiceBus")]
public class AzureServiceBusCollection : ICollectionFixture<AzureServiceBusContainerFixture>;
