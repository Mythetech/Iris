using FluentAssertions;
using Iris.Brokers.Amazon;
using Iris.Contracts.Brokers.Models.Amazon;
using Xunit;

namespace Iris.Brokers.Test;

public class AwsProfileCatalogTests
{
    [Fact(DisplayName = "Profiles are listed by name with the region each one sets")]
    public void Lists_profiles_in_name_order()
    {
        using var profiles = new TempAwsProfiles("""
            [zeta]
            aws_access_key_id = AKIAZETA
            aws_secret_access_key = secret
            region = us-east-1

            [Alpha]
            aws_access_key_id = AKIAALPHA
            aws_secret_access_key = secret

            [default]
            aws_access_key_id = AKIADEFAULT
            aws_secret_access_key = secret
            region = eu-west-3
            """);

        var listing = new AwsProfileCatalog(profiles.Options).GetProfiles();

        listing.Readable.Should().BeTrue();
        listing.Profiles.Should().Equal(
            new AwsProfile("Alpha", null),
            new AwsProfile("default", "eu-west-3"),
            new AwsProfile("zeta", "us-east-1"));
    }

    [Fact(DisplayName = "No profile file means no profiles, not an error")]
    public void A_missing_file_lists_nothing()
    {
        using var profiles = new TempAwsProfiles(credentials: null);

        var listing = new AwsProfileCatalog(profiles.Options).GetProfiles();

        listing.Readable.Should().BeTrue();
        listing.Profiles.Should().BeEmpty();
    }

    [Fact(DisplayName = "A profile file that cannot be parsed is reported as unreadable, so the dialog still opens")]
    public void A_malformed_file_is_unreadable()
    {
        using var profiles = new TempAwsProfiles("this is not\n[an ini file\n===\n");

        var listing = new AwsProfileCatalog(profiles.Options).GetProfiles();

        listing.Readable.Should().BeFalse();
        listing.Profiles.Should().BeEmpty();
    }

    [Fact(DisplayName = "One profile the AWS SDK refuses to load is reported as unreadable, not as 'no profiles'")]
    public void A_broken_profile_is_unreadable()
    {
        // The SDK cannot list any profile while one of them is broken, though the good ones
        // still resolve by name. Saying "no profiles found" here would be untrue.
        using var profiles = new TempAwsProfiles(
            "[alpha]\naws_access_key_id = AKIAALPHA\naws_secret_access_key = secret\n",
            config: "[profile broken]\nsso_session = nope\nsso_account_id = 123456789012\nsso_role_name = ReadOnly\n");

        new AwsProfileCatalog(profiles.Options).GetProfiles().Readable.Should().BeFalse();
    }

    [Fact(DisplayName = "A profile file with a broken line does not put that line in the log")]
    public void A_malformed_file_does_not_leak_into_the_log()
    {
        using var profiles = new TempAwsProfiles("[alpha]\naws_access_key_id = AKIAALPHA\naws_secret_access_key SECRETVALUE123\n");
        var log = new CapturingLoggerFactory();

        new AwsProfileCatalog(profiles.Options, log).GetProfiles();

        log.Entries.Should().NotBeEmpty();
        log.Entries.Should().NotContain(entry => entry.Contains("SECRETVALUE123"));
    }
}
