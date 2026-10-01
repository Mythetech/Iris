namespace Iris.Contracts.Brokers.Models.Amazon;

/// <summary>
/// The values <see cref="ConnectionData.AuthMode"/> takes for an Amazon connection. A null or
/// empty mode means <see cref="AccessKeys"/>, which is what every connection saved before
/// the mode existed has.
/// </summary>
public static class AwsAuthModes
{
    public const string AccessKeys = "AccessKeys";

    public const string Profile = "Profile";

    public const string DefaultChain = "DefaultChain";
}
