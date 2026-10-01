namespace Iris.Brokers.Amazon;

public sealed class AwsProfileOptions
{
    /// <summary>
    /// A credentials-format file to read profiles from, with a file named <c>config</c>
    /// beside it read as well. Null means the AWS SDK's own locations, which is what the app
    /// uses; tests point this at a temp file so they never read real profiles.
    /// </summary>
    public string? ProfilesLocation { get; init; }
}
