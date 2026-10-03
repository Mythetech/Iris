using Iris.Brokers.Amazon;

namespace Iris.Brokers.Test;

/// <summary>
/// A credentials file, and optionally a config file beside it, in their own temp directory,
/// so a test never reads the developer's real profiles and two tests never share a file.
/// </summary>
internal sealed class TempAwsProfiles : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "iris-aws-" + Guid.NewGuid().ToString("N"));

    public TempAwsProfiles(string? credentials, string? config = null)
    {
        Directory.CreateDirectory(_directory);

        if (credentials is not null)
            File.WriteAllText(CredentialsPath, credentials);

        if (config is not null)
            File.WriteAllText(Path.Combine(_directory, "config"), config);
    }

    public string CredentialsPath => Path.Combine(_directory, "credentials");

    public AwsProfileOptions Options => new() { ProfilesLocation = CredentialsPath };

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
