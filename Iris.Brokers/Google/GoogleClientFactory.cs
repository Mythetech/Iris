using System.Text.Json;
using System.Text.RegularExpressions;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.PubSub.V1;
using Grpc.Core;
using Iris.Brokers.Exceptions;
using Iris.Brokers.Models;

namespace Iris.Brokers.Google;

/// <summary>
/// Decides how a Google connection authenticates and builds its clients. Every credential
/// source is a branch here and nowhere else, which is what lets a later sign-in flow be
/// added as one more branch.
/// </summary>
public static partial class GoogleClientFactory
{
    public const string ProductionHost = "pubsub.googleapis.com";

    private const string ApplicationDefaultCredentialsFileName = "application_default_credentials.json";

    // Every type Application Default Credentials itself accepts.
    private static readonly HashSet<string> AcceptedCredentialTypes = new(StringComparer.Ordinal)
    {
        JsonCredentialParameters.ServiceAccountCredentialType,
        JsonCredentialParameters.AuthorizedUserCredentialType,
        JsonCredentialParameters.ImpersonatedServiceAccountCredentialType,
        JsonCredentialParameters.ExternalAccountCredentialType,
        JsonCredentialParameters.ExternalAccountAuthorizedUserCredentialType,
    };

    public static Task<GoogleConnectionSettings> ResolveAsync(ConnectionData data, CancellationToken cancellationToken)
        => ResolveAsync(
            data,
            () => FindApplicationDefaultCredentialsFile(Environment.GetEnvironmentVariable),
            cancellationToken);

    /// <param name="findApplicationDefaultFile">
    /// Where this machine's Application Default Credentials file is, or null when there is
    /// none. A parameter so that tests do not depend on the machine they run on.
    /// </param>
    public static async Task<GoogleConnectionSettings> ResolveAsync(
        ConnectionData data, Func<string?> findApplicationDefaultFile, CancellationToken cancellationToken)
    {
        switch (ChosenSource(data))
        {
            case GoogleCredentialSource.Emulator:
            {
                var host = NormalizeEmulatorHost(data.Uri);
                var projectId = RequireProjectId(data.ProjectId, fallback: null);
                return new GoogleConnectionSettings(
                    GoogleCredentialSource.Emulator, projectId, $"{host}/projects/{projectId}", EmulatorHost: host);
            }

            case GoogleCredentialSource.CredentialsFile:
            {
                if (string.IsNullOrWhiteSpace(data.CredentialsPath))
                    throw new InvalidConnectionException(PubSubErrors.CredentialsFileMissing);

                var path = data.CredentialsPath.Trim();
                var (type, fileProjectId) = await InspectCredentialsFileAsync(path, cancellationToken);
                var projectId = RequireProjectId(data.ProjectId, fileProjectId);
                return new GoogleConnectionSettings(
                    GoogleCredentialSource.CredentialsFile, projectId, ProductionAddress(projectId),
                    CredentialsPath: path, CredentialType: type);
            }

            default:
            {
                var projectId = RequireProjectId(data.ProjectId, fallback: null);

                if (findApplicationDefaultFile() is not { } file)
                {
                    return new GoogleConnectionSettings(
                        GoogleCredentialSource.ApplicationDefault, projectId, ProductionAddress(projectId));
                }

                try
                {
                    var (type, _) = await InspectCredentialsFileAsync(file, cancellationToken);
                    return new GoogleConnectionSettings(
                        GoogleCredentialSource.ApplicationDefault, projectId, ProductionAddress(projectId),
                        CredentialsPath: file, CredentialType: type);
                }
                catch (InvalidConnectionException ex)
                {
                    throw new InvalidConnectionException(PubSubErrors.ApplicationDefaultCredentialsUnreadable, ex);
                }
            }
        }
    }

    /// <summary>
    /// Looks where Google's own library looks, in its order: the file an environment variable
    /// names, then the one <c>gcloud auth application-default login</c> writes.
    /// </summary>
    public static string? FindApplicationDefaultCredentialsFile(Func<string, string?> environment)
    {
        var named = environment("GOOGLE_APPLICATION_CREDENTIALS");
        if (!string.IsNullOrWhiteSpace(named))
            return named;

        string? gcloud = null;

        if (!string.IsNullOrEmpty(environment("APPDATA")))
            gcloud = Path.Combine(environment("APPDATA")!, "gcloud", ApplicationDefaultCredentialsFileName);
        else if (!string.IsNullOrEmpty(environment("HOME")))
            gcloud = Path.Combine(environment("HOME")!, ".config", "gcloud", ApplicationDefaultCredentialsFileName);

        return gcloud is not null && File.Exists(gcloud) ? gcloud : null;
    }

    public static async Task<(PublisherServiceApiClient Publisher, SubscriberServiceApiClient Subscriber)> CreateClientsAsync(
        GoogleConnectionSettings settings, CancellationToken cancellationToken)
    {
        var publisher = new PublisherServiceApiClientBuilder();
        var subscriber = new SubscriberServiceApiClientBuilder();

        if (settings.Source == GoogleCredentialSource.Emulator)
        {
            publisher.Endpoint = settings.EmulatorHost;
            publisher.ChannelCredentials = ChannelCredentials.Insecure;
            subscriber.Endpoint = settings.EmulatorHost;
            subscriber.ChannelCredentials = ChannelCredentials.Insecure;
        }
        else
        {
            var credential = await LoadCredentialAsync(settings, cancellationToken);
            publisher.GoogleCredential = credential;
            subscriber.GoogleCredential = credential;
        }

        return (await publisher.BuildAsync(cancellationToken), await subscriber.BuildAsync(cancellationToken));
    }

    public static async Task<GoogleCredential> LoadCredentialAsync(
        GoogleConnectionSettings settings, CancellationToken cancellationToken)
    {
        GoogleCredential credential;

        if (settings.CredentialsPath is not null)
        {
            // Application Default Credentials come through here too whenever they are a file.
            // The library caches its own lookup, a failure included, for the life of the
            // process, so a login made after a failed connect would otherwise not be seen
            // until Iris was restarted.
            try
            {
                credential = await CredentialFactory.FromFileAsync(
                    settings.CredentialsPath, settings.CredentialType!, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The file named a known credential type but its contents did not hold up: a
                // truncated key, a missing field. The library reports those with whatever
                // exception its parser happened to throw.
                throw new InvalidConnectionException(
                    settings.Source == GoogleCredentialSource.CredentialsFile
                        ? PubSubErrors.NotACredentialsFile
                        : PubSubErrors.ApplicationDefaultCredentialsUnreadable,
                    ex);
            }
        }
        else
        {
            // No file on this machine. What is left is the library's own lookup, which also
            // knows about the metadata server of a cloud host.
            try
            {
                credential = await GoogleCredential.GetApplicationDefaultAsync(cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidConnectionException(PubSubErrors.NoApplicationDefaultCredentials, ex);
            }
        }

        // A gcloud user login carries no project to bill API calls to unless one was set, and
        // Pub/Sub refuses such calls outright. gcloud's own answer is the project being worked
        // on, so that is the answer here. Service accounts bill to their own project and are
        // left alone.
        if (credential.UnderlyingCredential is UserCredential && string.IsNullOrEmpty(credential.QuotaProject))
            credential = credential.CreateWithQuotaProject(settings.ProjectId);

        return credential;
    }

    /// <summary>
    /// The source the user chose. A payload that does not say, which only code-built ones do,
    /// is read the way it was before the choice was carried: from which field is filled in.
    /// </summary>
    private static GoogleCredentialSource ChosenSource(ConnectionData data)
    {
        if (string.IsNullOrWhiteSpace(data.CredentialSource))
        {
            if (!string.IsNullOrWhiteSpace(data.Uri))
                return GoogleCredentialSource.Emulator;

            return string.IsNullOrWhiteSpace(data.CredentialsPath)
                ? GoogleCredentialSource.ApplicationDefault
                : GoogleCredentialSource.CredentialsFile;
        }

        // Names only. Enum.TryParse would also accept "2".
        var name = Enum.GetNames<GoogleCredentialSource>()
            .FirstOrDefault(n => n.Equals(data.CredentialSource.Trim(), StringComparison.OrdinalIgnoreCase));

        return name is null
            ? throw new InvalidConnectionException(PubSubErrors.CredentialSourceUnknown)
            : Enum.Parse<GoogleCredentialSource>(name);
    }

    private static async Task<(string Type, string? ProjectId)> InspectCredentialsFileAsync(
        string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            throw new InvalidConnectionException(PubSubErrors.CredentialsFileMissing);

        try
        {
            await using var stream = File.OpenRead(path);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || ReadString(root, "type") is not { } type
                || !AcceptedCredentialTypes.Contains(type))
            {
                throw new InvalidConnectionException(PubSubErrors.NotACredentialsFile);
            }

            return (type, ReadString(root, "project_id"));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidConnectionException(PubSubErrors.NotACredentialsFile, ex);
        }
    }

    private static string? ReadString(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string RequireProjectId(string? fromForm, string? fallback)
    {
        var projectId = string.IsNullOrWhiteSpace(fromForm) ? fallback : fromForm.Trim();

        return string.IsNullOrWhiteSpace(projectId)
            ? throw new InvalidConnectionException(PubSubErrors.ProjectIdRequired)
            : projectId;
    }

    private static string ProductionAddress(string projectId) => $"{ProductionHost}/projects/{projectId}";

    private static string NormalizeEmulatorHost(string? uri)
    {
        var host = (uri ?? string.Empty).Trim();

        if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            host = host["http://".Length..];

        host = host.TrimEnd('/');

        return IsHostAndPort(host)
            ? host
            : throw new InvalidConnectionException(PubSubErrors.EmulatorHostInvalid);
    }

    // Checked here because the gRPC client throws on a host it cannot parse, and it does so
    // with an exception the connection dialog does not handle.
    private static bool IsHostAndPort(string value)
    {
        var match = HostAndPort().Match(value);

        return match.Success
               && int.Parse(match.Groups["port"].Value) is >= 1 and <= 65535
               && Uri.TryCreate($"http://{value}", UriKind.Absolute, out _);
    }

    [GeneratedRegex(@"^(\[[0-9A-Fa-f:.]+\]|[^\s/:\[\]]+):(?<port>\d{1,5})$")]
    private static partial Regex HostAndPort();
}
