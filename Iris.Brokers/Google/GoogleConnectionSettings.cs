namespace Iris.Brokers.Google;

public enum GoogleCredentialSource
{
    ApplicationDefault,
    CredentialsFile,
    Emulator,
}

/// <summary>
/// Everything a Pub/Sub connection was built from, minus the credential itself. Holds a
/// path, never key material, so it is safe to log.
/// </summary>
public sealed record GoogleConnectionSettings(
    GoogleCredentialSource Source,
    string ProjectId,
    string Address,
    string? EmulatorHost = null,
    string? CredentialsPath = null,
    string? CredentialType = null);
