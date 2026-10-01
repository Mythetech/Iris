namespace Iris.Contracts.Brokers.Models.Amazon;

/// <summary>
/// The AWS profiles available on the machine Iris is running on. It lives in Contracts so the
/// component layer can ask for the list without referencing the AWS SDK; a host that shows
/// the Amazon connection form registers an implementation.
/// </summary>
public interface IAwsProfileCatalog
{
    AwsProfileListing GetProfiles();
}
