using Google.Apis.Auth.OAuth2.Responses;
using Grpc.Core;

namespace Iris.Brokers.Google;

public enum PubSubOperation
{
    Admin,
    Publish,
    Pull,
}

/// <summary>
/// The only place Pub/Sub failures become user-facing text. The service layer shows an
/// exception's message verbatim, so the SDK's own wording must never reach it.
/// </summary>
public static class PubSubErrors
{
    public const string NoApplicationDefaultCredentials =
        "No Application Default Credentials found. Run gcloud auth application-default login, or choose a credentials file.";

    public const string CredentialsFileMissing = "No credentials file at that path.";

    public const string NotACredentialsFile = "That file is not a Google credentials file.";

    public const string ProjectIdRequired = "Enter a project ID.";

    public const string EmulatorHostInvalid = "Enter the emulator host as host:port, for example localhost:8085.";

    public const string ApplicationDefaultCredentialsUnreadable =
        "The Application Default Credentials on this machine could not be read. Run gcloud auth application-default login again.";

    public const string CredentialSourceUnknown =
        "This connection uses a credentials option this version of Iris does not support.";

    public const string ConnectFailed = "The Pub/Sub connection could not be created.";

    public const string EndpointNameRequired = "Choose a topic or subscription first.";

    private const string CredentialsRejected =
        "Google rejected the credentials. Run gcloud auth application-default login again, or check the credentials file, then connect again.";

    public static string EndpointNameInvalid(string name)
        => $"'{name}' is not a valid Pub/Sub topic or subscription name.";

    public static string Describe(RpcException exception, GoogleConnectionSettings settings, PubSubOperation operation)
        // An expired or revoked login fails while the access token is being refreshed, before
        // Pub/Sub is asked anything, so it arrives under whatever status the client wraps it
        // in rather than as Unauthenticated.
        => IsTokenRefusal(exception) ? CredentialsRejected : exception.StatusCode switch
        {
            StatusCode.Unauthenticated => CredentialsRejected,
            StatusCode.PermissionDenied =>
                $"These credentials are not allowed to do that on project '{settings.ProjectId}'. Listing needs the Pub/Sub Viewer role; publishing and receiving need Publisher and Subscriber.",
            StatusCode.NotFound =>
                $"Project, topic, or subscription not found. Check the project ID '{settings.ProjectId}'.",
            StatusCode.Unavailable or StatusCode.DeadlineExceeded when settings.Source == GoogleCredentialSource.Emulator =>
                $"Nothing answered at {settings.EmulatorHost}. Is the Pub/Sub emulator running?",
            StatusCode.Unavailable or StatusCode.DeadlineExceeded =>
                "Could not reach Pub/Sub. Check the network connection.",
            StatusCode.FailedPrecondition when operation == PubSubOperation.Pull =>
                "This subscription does not deliver by pull, so it cannot be read.",
            StatusCode.InvalidArgument when operation == PubSubOperation.Publish =>
                "Pub/Sub rejected the message. A message needs a body or at least one attribute, attribute values are limited to 1024 bytes, and a message to 10 MB.",
            _ => $"The Pub/Sub request failed ({exception.StatusCode}).",
        };

    private static bool IsTokenRefusal(RpcException exception)
    {
        for (var cause = exception.InnerException ?? exception.Status.DebugException; cause is not null; cause = cause.InnerException)
        {
            if (cause is TokenResponseException)
                return true;
        }

        return false;
    }
}
