using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.Runtime.Credentials;
using Iris.Brokers.Exceptions;
using Iris.Brokers.Models;
using AwsAuthModes = Iris.Contracts.Brokers.Models.Amazon.AwsAuthModes;

namespace Iris.Brokers.Amazon;

/// <summary>
/// Turns connection data into what an AWS client needs. Everything it can throw is an
/// <see cref="InvalidConnectionException"/> carrying a message fit to show the user.
/// </summary>
public class AwsCredentialsFactory
{
    private readonly AwsProfileOptions _options;

    public AwsCredentialsFactory(AwsProfileOptions? options = null)
    {
        _options = options ?? new AwsProfileOptions();
    }

    public AWSCredentials ResolveCredentials(ConnectionData data)
    {
        switch (ModeOf(data))
        {
            case AwsAuthModes.AccessKeys:
                if (string.IsNullOrWhiteSpace(data.Username) || string.IsNullOrWhiteSpace(data.Password))
                    throw new InvalidConnectionException(AwsConnectionMessages.MissingAccessKeys);

                return new BasicAWSCredentials(data.Username, data.Password);

            case AwsAuthModes.Profile:
                return ProfileCredentials(data.Profile);

            case AwsAuthModes.DefaultChain:
                return DefaultChain(() => DefaultAWSCredentialsIdentityResolver.GetCredentials());

            default:
                throw new InvalidConnectionException(AwsConnectionMessages.UnsupportedMode);
        }
    }

    public RegionEndpoint ResolveRegion(ConnectionData data)
    {
        if (!string.IsNullOrWhiteSpace(data.Region))
            return RegionEndpoint.GetBySystemName(data.Region);

        var region = ModeOf(data) switch
        {
            AwsAuthModes.Profile => ProfileRegion(data.Profile),
            AwsAuthModes.DefaultChain => DefaultChain(() => FallbackRegionFactory.GetRegionEndpoint()),
            _ => null,
        };

        return region ?? throw new InvalidConnectionException(AwsConnectionMessages.MissingRegion);
    }

    private static string ModeOf(ConnectionData data)
        => string.IsNullOrWhiteSpace(data.AuthMode) ? AwsAuthModes.AccessKeys : data.AuthMode;

    private AWSCredentials ProfileCredentials(string? profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
            throw new InvalidConnectionException(AwsConnectionMessages.MissingProfile);

        return ReadProfiles(profileName, chain =>
        {
            if (chain.TryGetAWSCredentials(profileName, out var credentials))
                return credentials;

            // A profile can exist and still yield nothing, for example one that only sets a
            // region. Telling that apart from a missing profile is the difference between
            // "fix your config" and "you mistyped the name".
            throw new InvalidConnectionException(chain.TryGetProfile(profileName, out _)
                ? AwsConnectionMessages.ProfileUnusable(profileName)
                : AwsConnectionMessages.ProfileNotFound(profileName));
        });
    }

    private RegionEndpoint? ProfileRegion(string? profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
            return null;

        return ReadProfiles(profileName, chain => chain.TryGetProfile(profileName, out var profile) ? profile.Region : null);
    }

    // The SDK's default chain reads the default profile as well as the environment, so a
    // broken profile surfaces here as whatever the SDK throws for it.
    private static T DefaultChain<T>(Func<T> resolve)
    {
        try
        {
            return resolve();
        }
        catch (Exception ex)
        {
            throw new InvalidConnectionException(AwsConnectionMessages.DefaultCredentialsFailed, ex);
        }
    }

    private T ReadProfiles<T>(string profileName, Func<CredentialProfileStoreChain, T> read)
    {
        try
        {
            return read(new CredentialProfileStoreChain(_options.ProfilesLocation));
        }
        catch (InvalidConnectionException)
        {
            throw;
        }
        catch (AmazonClientException ex)
        {
            // The files parsed, but this profile's settings do not add up, for example an
            // sso_session naming a section that is not there.
            throw new InvalidConnectionException(AwsConnectionMessages.ProfileUnusable(profileName), ex);
        }
        catch (Exception ex)
        {
            throw new InvalidConnectionException(AwsConnectionMessages.ProfilesUnreadable, ex);
        }
    }
}
