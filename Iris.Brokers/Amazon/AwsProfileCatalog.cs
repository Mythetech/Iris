using Amazon.Runtime.CredentialManagement;
using Iris.Contracts.Brokers.Models.Amazon;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Iris.Brokers.Amazon;

public class AwsProfileCatalog : IAwsProfileCatalog
{
    private readonly AwsProfileOptions _options;
    private readonly ILogger<AwsProfileCatalog> _logger;

    public AwsProfileCatalog(AwsProfileOptions? options = null, ILoggerFactory? loggerFactory = null)
    {
        _options = options ?? new AwsProfileOptions();
        _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<AwsProfileCatalog>();
    }

    public AwsProfileListing GetProfiles()
    {
        try
        {
            // The SDK already leaves out profiles that cannot produce credentials and
            // sso-session sections, so everything listed here is something a user can pick.
            var profiles = new CredentialProfileStoreChain(_options.ProfilesLocation)
                .ListProfiles()
                .Select(profile => new AwsProfile(profile.Name, profile.Region?.SystemName))
                .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new AwsProfileListing(profiles, Readable: true);
        }
        catch (Exception ex)
        {
            // The list feeds a dropdown, so a broken config file must not stop the dialog
            // opening. One broken profile is enough: the SDK lists none while any is invalid.
            // The type only: a parse error quotes the line it could not read, and that line
            // can be the one holding a secret key.
            _logger.LogWarning("Could not read AWS profiles: {Reason}", ex.GetType().Name);
            return AwsProfileListing.Unreadable;
        }
    }
}
