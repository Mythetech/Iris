using FluentAssertions;
using Google.Apis.Auth.OAuth2.Responses;
using Grpc.Core;
using Iris.Brokers.Google;
using Xunit;

namespace Iris.Brokers.Test.Google;

public class PubSubErrorsTests
{
    private static readonly GoogleConnectionSettings Real = new(
        GoogleCredentialSource.ApplicationDefault, "my-project", "pubsub.googleapis.com/projects/my-project");

    private static readonly GoogleConnectionSettings Emulator = new(
        GoogleCredentialSource.Emulator, "my-project", "localhost:8085/projects/my-project", EmulatorHost: "localhost:8085");

    private static RpcException Rpc(StatusCode code) => new(new Status(code, "the SDK's own wording"));

    [Theory]
    [InlineData(StatusCode.Unauthenticated, PubSubOperation.Admin, "Google rejected the credentials. Run gcloud auth application-default login again, or check the credentials file, then connect again.")]
    [InlineData(StatusCode.PermissionDenied, PubSubOperation.Admin, "These credentials are not allowed to do that on project 'my-project'. Listing needs the Pub/Sub Viewer role; publishing and receiving need Publisher and Subscriber.")]
    [InlineData(StatusCode.NotFound, PubSubOperation.Publish, "Project, topic, or subscription not found. Check the project ID 'my-project'.")]
    [InlineData(StatusCode.Unavailable, PubSubOperation.Admin, "Could not reach Pub/Sub. Check the network connection.")]
    [InlineData(StatusCode.DeadlineExceeded, PubSubOperation.Admin, "Could not reach Pub/Sub. Check the network connection.")]
    [InlineData(StatusCode.FailedPrecondition, PubSubOperation.Pull, "This subscription does not deliver by pull, so it cannot be read.")]
    [InlineData(StatusCode.InvalidArgument, PubSubOperation.Publish, "Pub/Sub rejected the message. A message needs a body or at least one attribute, attribute values are limited to 1024 bytes, and a message to 10 MB.")]
    [InlineData(StatusCode.Internal, PubSubOperation.Admin, "The Pub/Sub request failed (Internal).")]
    public void Each_status_has_fixed_wording(StatusCode status, PubSubOperation operation, string expected)
    {
        PubSubErrors.Describe(Rpc(status), Real, operation).Should().Be(expected);
    }

    [Theory]
    [InlineData(StatusCode.Unavailable)]
    [InlineData(StatusCode.DeadlineExceeded)]
    public void An_unreachable_emulator_is_named_by_its_host(StatusCode status)
    {
        PubSubErrors.Describe(Rpc(status), Emulator, PubSubOperation.Admin)
            .Should().Be("Nothing answered at localhost:8085. Is the Pub/Sub emulator running?");
    }

    [Theory]
    [InlineData(StatusCode.FailedPrecondition, PubSubOperation.Publish)]
    [InlineData(StatusCode.InvalidArgument, PubSubOperation.Pull)]
    public void Wording_tied_to_one_operation_does_not_leak_into_another(StatusCode status, PubSubOperation operation)
    {
        PubSubErrors.Describe(Rpc(status), Real, operation)
            .Should().Be($"The Pub/Sub request failed ({status}).");
    }

    [Theory]
    [InlineData(StatusCode.Internal)]
    [InlineData(StatusCode.Unknown)]
    [InlineData(StatusCode.Unavailable)]
    public void A_refused_token_request_is_rejected_credentials_whatever_status_it_arrives_under(StatusCode status)
    {
        // An expired or revoked login fails when the token is refreshed, before Pub/Sub is
        // asked anything, so it never arrives as Unauthenticated.
        var refused = new TokenResponseException(new TokenErrorResponse { Error = "invalid_grant" });
        var exception = new RpcException(new Status(status, "the SDK's own wording", refused));

        PubSubErrors.Describe(exception, Real, PubSubOperation.Admin)
            .Should().Be("Google rejected the credentials. Run gcloud auth application-default login again, or check the credentials file, then connect again.");
    }

    [Fact]
    public void The_SDK_message_never_reaches_the_user()
    {
        foreach (var status in Enum.GetValues<StatusCode>())
        foreach (var operation in Enum.GetValues<PubSubOperation>())
        {
            PubSubErrors.Describe(Rpc(status), Real, operation).Should().NotContain("the SDK's own wording");
        }
    }
}
