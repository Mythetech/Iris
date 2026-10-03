using Amazon.SQS;
using Iris.Brokers.Models;
using AwsAuthModes = Iris.Contracts.Brokers.Models.Amazon.AwsAuthModes;

namespace Iris.Brokers.Amazon;

/// <summary>
/// Everything an AWS connect failure can say to the user. They are fixed strings because the
/// alternative, the exception's own message, can carry account ids, ARNs and file paths.
/// </summary>
public static class AwsConnectionMessages
{
    public const string MissingAccessKeys = "Enter an access key and a secret access key.";

    public const string MissingProfile = "Select an AWS profile.";

    public const string MissingRegion = "Select a region.";

    public const string UnsupportedMode = "Unsupported AWS authentication mode.";

    public const string ProfilesUnreadable = "Your AWS credentials or config file could not be read. Check it for errors.";

    public const string DefaultCredentialsFailed =
        "Could not connect to AWS with the default credentials. Create a default profile with 'aws configure', or sign in again, then connect again. If that does not work, restart Iris.";

    public const string AccessKeysFailed = "Could not connect to AWS. Check your network connection and the selected region.";

    public static string ProfileNotFound(string profile) => $"AWS profile '{profile}' was not found.";

    public static string ProfileUnusable(string profile) =>
        $"AWS profile '{profile}' has no usable credentials. Check its settings in your AWS config file.";

    public static string ProfileFailed(string profile) =>
        $"Could not connect to AWS with profile '{profile}'. If the profile signs in through SSO or the console, run 'aws sso login --profile {profile}' or 'aws login --profile {profile}' in a terminal, then connect again.";

    public static string Rejected(string region) =>
        $"AWS rejected the request. Check that the credentials are valid and allowed to list SQS queues in {region}.";

    /// <summary>
    /// The message for a connect attempt that failed after its settings resolved. SQS answering
    /// with an error means the request arrived and was refused; anything else never got a
    /// usable answer, so the message points at the likely fix for the connection's mode.
    /// </summary>
    public static string ForFailure(Exception exception, ConnectionData data, string region)
    {
        if (exception is AmazonSQSException)
            return Rejected(region);

        return data.AuthMode switch
        {
            AwsAuthModes.Profile => ProfileFailed(data.Profile ?? string.Empty),
            AwsAuthModes.DefaultChain => DefaultCredentialsFailed,
            _ => AccessKeysFailed,
        };
    }
}
