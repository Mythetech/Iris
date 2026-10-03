using FluentAssertions;
using Iris.Brokers.Amazon;
using Iris.Brokers.Exceptions;
using Iris.Brokers.Models;
using Xunit;
using AwsAuthModes = Iris.Contracts.Brokers.Models.Amazon.AwsAuthModes;

namespace Iris.Brokers.Test;

public class AwsCredentialsFactoryTests
{
    private const string Profiles = """
        [alpha]
        aws_access_key_id = AKIAALPHA
        aws_secret_access_key = alpha-secret
        region = eu-west-3

        [beta]
        aws_access_key_id = AKIABETA
        aws_secret_access_key = beta-secret

        [region-only]
        region = us-east-1
        """;

    private static ConnectionData Keys(string? mode, string? key = "AKIAKEYS", string? secret = "keys-secret") => new()
    {
        AuthMode = mode,
        Username = key,
        Password = secret,
        Region = "us-east-1",
    };

    private static ConnectionData Profile(string? name, string? region = null) => new()
    {
        AuthMode = AwsAuthModes.Profile,
        Profile = name,
        Region = region,
    };

    [Theory(DisplayName = "No mode, a blank mode and AccessKeys all use the keys that were entered")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(AwsAuthModes.AccessKeys)]
    public void Access_keys_are_the_default(string? mode)
    {
        var credentials = new AwsCredentialsFactory().ResolveCredentials(Keys(mode)).GetCredentials();

        credentials.AccessKey.Should().Be("AKIAKEYS");
        credentials.SecretKey.Should().Be("keys-secret");
    }

    [Theory(DisplayName = "A missing access key or secret is refused")]
    [InlineData(null, "secret")]
    [InlineData("", "secret")]
    [InlineData("AKIAKEYS", null)]
    [InlineData("AKIAKEYS", "  ")]
    public void Missing_keys_are_refused(string? key, string? secret)
    {
        var act = () => new AwsCredentialsFactory().ResolveCredentials(Keys(AwsAuthModes.AccessKeys, key, secret));

        act.Should().Throw<InvalidConnectionException>()
            .Which.Message.Should().Be(AwsConnectionMessages.MissingAccessKeys);
    }

    [Theory(DisplayName = "A profile connection uses that profile's credentials, not a neighbour's")]
    [InlineData("alpha", "AKIAALPHA")]
    [InlineData("beta", "AKIABETA")]
    public void Profile_credentials_come_from_the_named_profile(string name, string expectedKey)
    {
        using var profiles = new TempAwsProfiles(Profiles);

        var credentials = new AwsCredentialsFactory(profiles.Options).ResolveCredentials(Profile(name)).GetCredentials();

        credentials.AccessKey.Should().Be(expectedKey);
    }

    [Fact(DisplayName = "Keys left over from access key mode are ignored in profile mode")]
    public void Profile_mode_ignores_entered_keys()
    {
        using var profiles = new TempAwsProfiles(Profiles);
        var data = Profile("alpha");
        data.Username = "AKIASTALE";
        data.Password = "stale-secret";

        var credentials = new AwsCredentialsFactory(profiles.Options).ResolveCredentials(data).GetCredentials();

        credentials.AccessKey.Should().Be("AKIAALPHA");
    }

    [Theory(DisplayName = "Profile mode with no profile chosen is refused")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void A_blank_profile_is_refused(string? name)
    {
        using var profiles = new TempAwsProfiles(Profiles);

        var act = () => new AwsCredentialsFactory(profiles.Options).ResolveCredentials(Profile(name));

        act.Should().Throw<InvalidConnectionException>()
            .Which.Message.Should().Be(AwsConnectionMessages.MissingProfile);
    }

    [Fact(DisplayName = "A profile that does not exist is named in the refusal")]
    public void An_unknown_profile_is_refused()
    {
        using var profiles = new TempAwsProfiles(Profiles);

        var act = () => new AwsCredentialsFactory(profiles.Options).ResolveCredentials(Profile("missing"));

        act.Should().Throw<InvalidConnectionException>()
            .Which.Message.Should().Be(AwsConnectionMessages.ProfileNotFound("missing"));
    }

    [Fact(DisplayName = "A profile with no credentials in it says so rather than 'not found'")]
    public void A_profile_without_credentials_is_refused()
    {
        using var profiles = new TempAwsProfiles(Profiles);

        var act = () => new AwsCredentialsFactory(profiles.Options).ResolveCredentials(Profile("region-only"));

        act.Should().Throw<InvalidConnectionException>()
            .Which.Message.Should().Be(AwsConnectionMessages.ProfileUnusable("region-only"));
    }

    [Fact(DisplayName = "A profile file that cannot be parsed is reported, not thrown raw")]
    public void A_malformed_profile_file_is_refused()
    {
        using var profiles = new TempAwsProfiles("this is not\n[an ini file\n===\n");
        var factory = new AwsCredentialsFactory(profiles.Options);

        var credentials = () => factory.ResolveCredentials(Profile("alpha"));
        var region = () => factory.ResolveRegion(Profile("alpha"));

        credentials.Should().Throw<InvalidConnectionException>()
            .Which.Message.Should().Be(AwsConnectionMessages.ProfilesUnreadable);
        region.Should().Throw<InvalidConnectionException>()
            .Which.Message.Should().Be(AwsConnectionMessages.ProfilesUnreadable);
    }

    [Fact(DisplayName = "A profile the AWS SDK refuses to load is reported, not thrown raw")]
    public void A_profile_the_sdk_rejects_is_refused()
    {
        // An sso_session that names a section the file does not have. The SDK throws its own
        // exception for this, from the lookup itself rather than on first use.
        using var profiles = new TempAwsProfiles(Profiles, config: """
            [profile broken]
            sso_session = nope
            sso_account_id = 123456789012
            sso_role_name = ReadOnly
            """);
        var factory = new AwsCredentialsFactory(profiles.Options);

        var credentials = () => factory.ResolveCredentials(Profile("broken"));
        var region = () => factory.ResolveRegion(Profile("broken"));

        credentials.Should().Throw<InvalidConnectionException>()
            .Which.Message.Should().Be(AwsConnectionMessages.ProfileUnusable("broken"));
        region.Should().Throw<InvalidConnectionException>();
    }

    [Fact(DisplayName = "One broken profile does not stop its neighbours resolving")]
    public void A_broken_neighbour_does_not_matter()
    {
        using var profiles = new TempAwsProfiles(Profiles, config: """
            [profile broken]
            sso_session = nope
            sso_account_id = 123456789012
            sso_role_name = ReadOnly
            """);

        var credentials = new AwsCredentialsFactory(profiles.Options).ResolveCredentials(Profile("alpha")).GetCredentials();

        credentials.AccessKey.Should().Be("AKIAALPHA");
    }

    [Fact(DisplayName = "A mode Iris does not know is refused")]
    public void An_unknown_mode_is_refused()
    {
        var act = () => new AwsCredentialsFactory().ResolveCredentials(Keys("Kerberos"));

        act.Should().Throw<InvalidConnectionException>()
            .Which.Message.Should().Be(AwsConnectionMessages.UnsupportedMode);
    }

    [Fact(DisplayName = "A region that was chosen wins over the profile's own")]
    public void An_explicit_region_wins()
    {
        using var profiles = new TempAwsProfiles(Profiles);

        var region = new AwsCredentialsFactory(profiles.Options).ResolveRegion(Profile("alpha", region: "us-west-2"));

        region.SystemName.Should().Be("us-west-2");
    }

    [Theory(DisplayName = "A blank region in profile mode falls back to the profile's region")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void A_blank_region_uses_the_profiles(string? blank)
    {
        using var profiles = new TempAwsProfiles(Profiles);

        var region = new AwsCredentialsFactory(profiles.Options).ResolveRegion(Profile("alpha", region: blank));

        region.SystemName.Should().Be("eu-west-3");
    }

    [Fact(DisplayName = "A blank region with a profile that has none is refused")]
    public void No_region_anywhere_is_refused()
    {
        using var profiles = new TempAwsProfiles(Profiles);

        var act = () => new AwsCredentialsFactory(profiles.Options).ResolveRegion(Profile("beta"));

        act.Should().Throw<InvalidConnectionException>()
            .Which.Message.Should().Be(AwsConnectionMessages.MissingRegion);
    }

    [Fact(DisplayName = "Access keys need a region chosen")]
    public void Access_keys_need_a_region()
    {
        var data = Keys(AwsAuthModes.AccessKeys);
        data.Region = "";

        var act = () => new AwsCredentialsFactory().ResolveRegion(data);

        act.Should().Throw<InvalidConnectionException>()
            .Which.Message.Should().Be(AwsConnectionMessages.MissingRegion);
    }
}
