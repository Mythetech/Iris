using Amazon.Runtime;
using Amazon.SQS;
using FluentAssertions;
using Iris.Brokers.Amazon;
using Iris.Brokers.Models;
using Xunit;
using AwsAuthModes = Iris.Contracts.Brokers.Models.Amazon.AwsAuthModes;

namespace Iris.Brokers.Test;

/// <summary>
/// The split is deliberately coarse. SQS answering with an error means the credentials reached
/// it and were refused. Anything else means no usable answer came back, which covers an
/// expired session and a dead network alike, so the message names the likely fix for the
/// connection's mode without claiming which one it was.
/// </summary>
public class AwsConnectionMessagesTests
{
    private static ConnectionData Data(string? mode, string? profile = null) => new() { AuthMode = mode, Profile = profile };

    [Theory(DisplayName = "An answer from SQS is reported as a rejection, whatever the mode")]
    [InlineData(null)]
    [InlineData(AwsAuthModes.AccessKeys)]
    [InlineData(AwsAuthModes.Profile)]
    [InlineData(AwsAuthModes.DefaultChain)]
    public void Sqs_errors_are_rejections(string? mode)
    {
        var message = AwsConnectionMessages.ForFailure(new AmazonSQSException("denied"), Data(mode, "dev"), "eu-west-1");

        message.Should().Be(AwsConnectionMessages.Rejected("eu-west-1"));
    }

    [Fact(DisplayName = "Any other failure in profile mode points at signing in again")]
    public void Profile_failures_name_the_profile()
    {
        var message = AwsConnectionMessages.ForFailure(new AmazonClientException("token expired"), Data(AwsAuthModes.Profile, "dev"), "eu-west-1");

        message.Should().Be(AwsConnectionMessages.ProfileFailed("dev"));
    }

    [Fact(DisplayName = "Any other failure in default mode points at the default profile")]
    public void Default_failures_point_at_the_default_profile()
    {
        var message = AwsConnectionMessages.ForFailure(new HttpRequestException("no route"), Data(AwsAuthModes.DefaultChain), "eu-west-1");

        message.Should().Be(AwsConnectionMessages.DefaultCredentialsFailed);
    }

    [Theory(DisplayName = "Any other failure with access keys points at the network and region")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(AwsAuthModes.AccessKeys)]
    public void Access_key_failures_point_at_the_network(string? mode)
    {
        var message = AwsConnectionMessages.ForFailure(new HttpRequestException("no route"), Data(mode), "eu-west-1");

        message.Should().Be(AwsConnectionMessages.AccessKeysFailed);
    }

    [Fact(DisplayName = "The default credentials message says a restart may be needed")]
    public void Default_failures_mention_restarting()
    {
        // Once the AWS SDK's default chain has looked and found nothing, it does not look
        // again in the same process: a credentials file created afterwards is not seen. So
        // "then connect again" alone would send a new user round in a circle.
        AwsConnectionMessages.DefaultCredentialsFailed.Should().Contain("restart Iris");
    }

    [Fact(DisplayName = "No message repeats what the exception said")]
    public void Exception_text_never_reaches_the_message()
    {
        var message = AwsConnectionMessages.ForFailure(new AmazonSQSException("arn:aws:iam::123456789012:user/secret-name"), Data(null), "eu-west-1");

        message.Should().NotContain("123456789012");
    }
}
