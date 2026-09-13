using System.Globalization;
using FluentAssertions;
using Iris.Brokers.Frameworks;
using Iris.Brokers.Models;
using Xunit;

namespace Iris.Brokers.Test.Frameworks;

public class WolverineAdapterTests
{
    private const string SampleJson = "{\"red\":1}";

    private static MessageRequest NewRequest(string? messageFullyQualifiedName = "MyApp.Messages.ColorChosen")
        => MessageRequest.Create(
            messageType: "ColorChosen",
            json: SampleJson,
            generateIrisHeaders: false,
            messageFullyQualifiedName: messageFullyQualifiedName,
            framework: "Wolverine");

    [Fact(DisplayName = "Body is the user JSON unchanged")]
    public void Body_Unchanged()
    {
        var request = NewRequest();

        new WolverineAdapter().CreateWrappedMessage(request).Should().Be(SampleJson);
    }

    [Fact(DisplayName = "Declared keys match what CreateWrappedMessage writes")]
    public void Keys_MatchWrite()
    {
        FrameworkKeyAssertions.AssertKeysMatchWrite(new WolverineAdapter(), NewRequest());
    }

    [Fact(DisplayName = "Message identity defaults to the fully qualified type name")]
    public void Identity_DefaultsToFullyQualifiedName()
    {
        var request = NewRequest();

        new WolverineAdapter().CreateWrappedMessage(request);

        request.TransportProperties.Type.Should().Be("MyApp.Messages.ColorChosen");
    }

    [Fact(DisplayName = "Falls back to the short message type when no fully qualified name is supplied")]
    public void Identity_FallsBackToMessageType_WhenNoFullName()
    {
        var request = NewRequest(messageFullyQualifiedName: null);

        new WolverineAdapter().CreateWrappedMessage(request);

        request.TransportProperties.Type.Should().Be("ColorChosen");
    }

    [Fact(DisplayName = "Sets content type, ids and Wolverine's envelope headers")]
    public void Writes_TransportProperties_And_Headers()
    {
        var request = NewRequest();

        new WolverineAdapter().CreateWrappedMessage(request);

        request.TransportProperties.ContentType.Should().Be("application/json");
        request.TransportProperties.MessageId.Should().MatchRegex(@"^[0-9a-fA-F-]{36}$");
        request.TransportProperties.CorrelationId.Should().Be(request.TransportProperties.MessageId);
        request.Headers[WolverineAdapter.ProtocolVersionHeader].Should().Be("1.0");
        request.Headers[WolverineAdapter.SourceHeader].Should().Be("iris");
        request.Headers[WolverineAdapter.ConversationIdHeader].Should().Be(request.TransportProperties.MessageId);
        DateTimeOffset.TryParseExact(request.Headers[WolverineAdapter.SentAtHeader], "yyyy-MM-dd HH:mm:ss:ffffff Z",
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _).Should().BeTrue();
    }

    [Fact(DisplayName = "Only the identity is required")]
    public void Keys_OnlyTypeRequired()
    {
        new WolverineAdapter().Keys.Where(k => k.IsRequired)
            .Should().ContainSingle(k => k.Property == TransportProperty.Type);
    }
}
