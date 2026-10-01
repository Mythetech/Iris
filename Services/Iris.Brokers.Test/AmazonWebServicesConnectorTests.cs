using Amazon;
using FluentAssertions;
using Iris.Brokers.Amazon;
using Iris.Brokers.Exceptions;
using Iris.Brokers.Models;
using Xunit;
using AwsAuthModes = Iris.Contracts.Brokers.Models.Amazon.AwsAuthModes;

namespace Iris.Brokers.Test;

/// <summary>
/// Only the paths that fail before any network call. What happens once SQS is reached is
/// covered by the message mapping tests and, against an emulator, by the integration suite.
/// </summary>
public class AmazonWebServicesConnectorTests
{
    [Fact(DisplayName = "Connecting with no keys fails with a message the dialog can show")]
    public async Task Missing_keys_fail_before_any_call()
    {
        var connector = new AmazonWebServicesConnector();

        var act = () => connector.ConnectAsync(new ConnectionData { Region = "us-east-1" });

        (await act.Should().ThrowAsync<InvalidConnectionException>())
            .Which.Message.Should().Be(AwsConnectionMessages.MissingAccessKeys);
    }

    [Fact(DisplayName = "Connecting with no region fails with a message the dialog can show")]
    public async Task A_missing_region_fails_before_any_call()
    {
        var connector = new AmazonWebServicesConnector();

        var act = () => connector.ConnectAsync(new ConnectionData { Username = "AKIAKEYS", Password = "secret" });

        (await act.Should().ThrowAsync<InvalidConnectionException>())
            .Which.Message.Should().Be(AwsConnectionMessages.MissingRegion);
    }

    [Fact(DisplayName = "The connector resolves profiles through the factory it was given")]
    public async Task An_unknown_profile_fails_before_any_call()
    {
        using var profiles = new TempAwsProfiles("[alpha]\naws_access_key_id = AKIAALPHA\naws_secret_access_key = secret\n");
        var connector = new AmazonWebServicesConnector(new AwsCredentialsFactory(profiles.Options));

        var act = () => connector.ConnectAsync(new ConnectionData
        {
            AuthMode = AwsAuthModes.Profile,
            Profile = "missing",
            Region = "us-east-1",
        });

        (await act.Should().ThrowAsync<InvalidConnectionException>())
            .Which.Message.Should().Be(AwsConnectionMessages.ProfileNotFound("missing"));
    }

    [Fact(DisplayName = "With nothing filled in for profile mode, the profile is asked for before the region")]
    public async Task The_profile_is_asked_for_first()
    {
        // Region may be left blank in profile mode, so "Select a region." would send the user
        // to the one field that was not the problem.
        var connector = new AmazonWebServicesConnector();

        var act = () => connector.ConnectAsync(new ConnectionData { AuthMode = AwsAuthModes.Profile });

        (await act.Should().ThrowAsync<InvalidConnectionException>())
            .Which.Message.Should().Be(AwsConnectionMessages.MissingProfile);
    }

    [Fact(DisplayName = "A profile file with a broken line does not put that line in the log")]
    public async Task A_malformed_file_does_not_leak_into_the_log()
    {
        // The SDK's parse error quotes the offending line, and the line most likely to be
        // mistyped is the one holding the secret.
        using var profiles = new TempAwsProfiles("[alpha]\naws_access_key_id = AKIAALPHA\naws_secret_access_key SECRETVALUE123\n");
        var log = new CapturingLoggerFactory();
        var connector = new AmazonWebServicesConnector(new AwsCredentialsFactory(profiles.Options), log);

        var act = () => connector.ConnectAsync(new ConnectionData { AuthMode = AwsAuthModes.Profile, Profile = "alpha", Region = "us-east-1" });

        (await act.Should().ThrowAsync<InvalidConnectionException>())
            .Which.Message.Should().Be(AwsConnectionMessages.ProfilesUnreadable);
        log.Entries.Should().NotBeEmpty();
        log.Entries.Should().NotContain(entry => entry.Contains("SECRETVALUE123"));
    }

    [Fact(DisplayName = "Only a cancellation the caller asked for is passed through as one")]
    public void Only_the_callers_cancellation_passes_through()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        // The SDK reports its own HTTP timeouts as cancellations. With a token nobody
        // cancelled, that is a failed connection, not a cancelled one.
        AmazonWebServicesConnector.IsCallerCancellation(new TaskCanceledException(), cancelled.Token).Should().BeTrue();
        AmazonWebServicesConnector.IsCallerCancellation(new TaskCanceledException(), CancellationToken.None).Should().BeFalse();
        AmazonWebServicesConnector.IsCallerCancellation(new HttpRequestException(), cancelled.Token).Should().BeFalse();
    }

    [Fact(DisplayName = "A queue URL gives the account's address")]
    public void The_address_comes_from_the_first_queue()
    {
        var address = AmazonWebServicesConnector.AddressFor(
            ["https://sqs.us-east-1.amazonaws.com/123456789012/orders", "https://sqs.us-east-1.amazonaws.com/123456789012/billing"],
            RegionEndpoint.USEast1);

        address.Should().Be("https://sqs.us-east-1.amazonaws.com/123456789012");
    }

    [Fact(DisplayName = "An account with no queues gets the region's address")]
    public void No_queues_falls_back_to_the_region()
    {
        // The SDK hands back null, not an empty list, when the response carries no queues.
        AmazonWebServicesConnector.AddressFor(null, RegionEndpoint.USEast1).Should().Be("us-east-1.amazonaws.com");
        AmazonWebServicesConnector.AddressFor([], RegionEndpoint.USEast1).Should().Be("us-east-1.amazonaws.com");
    }
}
