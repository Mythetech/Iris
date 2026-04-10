using FluentAssertions;
using Iris.Brokers.Frameworks;
using Iris.Brokers.Models;
using Xunit;
using RebusHeaders = Rebus.Messages.Headers;

namespace Iris.Brokers.Test.Frameworks;

public class RebusAdapterTests
{
    private const string SampleJson = "{\"id\":1,\"name\":\"hello\"}";
    private const string SampleType = "MyApp.Messages.Greeting";
    private const string SampleAssembly = "MyApp.Contracts";

    private static MessageRequest BuildRequest(
        Dictionary<string, string>? headers = null,
        string? assembly = SampleAssembly)
    {
        return MessageRequest.Create(
            messageType: SampleType,
            json: SampleJson,
            generateIrisHeaders: false,
            messageFullyQualifiedName: SampleType,
            framework: "Rebus",
            headers: headers,
            properties: null,
            messageAssemblyName: assembly);
    }

    [Fact(DisplayName = "Name returns Rebus")]
    public void Name_IsRebus()
    {
        new RebusAdapter().Name.Should().Be("Rebus");
    }

    [Fact(DisplayName = "Body is the user JSON unchanged")]
    public void Body_EqualsUserJson()
    {
        var request = BuildRequest();
        var body = new RebusAdapter().CreateWrappedMessage(request);
        body.Should().Be(SampleJson);
        request.Json.Should().Be(SampleJson);
    }

    [Fact(DisplayName = "All expected rbs2-* headers are populated")]
    public void Headers_AllRebusKeysPresent()
    {
        var request = BuildRequest();
        new RebusAdapter().CreateWrappedMessage(request);

        request.Headers.Should().ContainKey(RebusHeaders.MessageId);
        request.Headers.Should().ContainKey(RebusHeaders.CorrelationId);
        request.Headers.Should().ContainKey(RebusHeaders.CorrelationSequence);
        request.Headers.Should().ContainKey(RebusHeaders.SentTime);
        request.Headers.Should().ContainKey(RebusHeaders.ReturnAddress);
        request.Headers.Should().ContainKey(RebusHeaders.SenderAddress);
        request.Headers.Should().ContainKey(RebusHeaders.Type);
        request.Headers.Should().ContainKey(RebusHeaders.ContentType);
        request.Headers.Should().ContainKey(RebusHeaders.Intent);

        request.Headers[RebusHeaders.CorrelationSequence].Should().Be("0");
        request.Headers[RebusHeaders.ReturnAddress].Should().Be("iris");
        request.Headers[RebusHeaders.SenderAddress].Should().Be("iris");
        request.Headers[RebusHeaders.ContentType].Should().Be("application/json;charset=utf-8");
        request.Headers[RebusHeaders.Intent].Should().Be("p2p");
    }

    [Fact(DisplayName = "MessageId is a fresh GUID and CorrelationId mirrors it on first send")]
    public void Headers_MessageIdIsGuidAndMatchesCorrelationId()
    {
        var request = BuildRequest();
        new RebusAdapter().CreateWrappedMessage(request);

        var messageId = request.Headers[RebusHeaders.MessageId];
        messageId.Should().MatchRegex(@"^[0-9a-fA-F-]{36}$");
        request.Headers[RebusHeaders.CorrelationId].Should().Be(messageId);
    }

    [Fact(DisplayName = "rbs2-msg-type includes assembly name when provided")]
    public void Type_IncludesAssemblyName()
    {
        var request = BuildRequest();
        new RebusAdapter().CreateWrappedMessage(request);

        request.Headers[RebusHeaders.Type].Should().Be($"{SampleType}, {SampleAssembly}");
    }

    [Fact(DisplayName = "rbs2-msg-type falls back to FQN-only when assembly name is null")]
    public void Type_FallsBackToFqnWhenAssemblyMissing()
    {
        var request = BuildRequest(assembly: null);
        new RebusAdapter().CreateWrappedMessage(request);

        request.Headers[RebusHeaders.Type].Should().Be(SampleType);
    }

    [Fact(DisplayName = "Caller-supplied rbs2-msg-id wins over generated value")]
    public void Headers_UserSuppliedMessageIdWins()
    {
        var preset = "preset-id-1234";
        var request = BuildRequest(new Dictionary<string, string>
        {
            [RebusHeaders.MessageId] = preset,
        });

        new RebusAdapter().CreateWrappedMessage(request);

        request.Headers[RebusHeaders.MessageId].Should().Be(preset);
    }

    [Fact(DisplayName = "Existing iris-key header is preserved alongside rbs2-* headers")]
    public void Headers_IrisKeyPreserved()
    {
        var request = MessageRequest.Create(
            messageType: SampleType,
            json: SampleJson,
            generateIrisHeaders: true,
            messageFullyQualifiedName: SampleType,
            framework: "Rebus",
            messageAssemblyName: SampleAssembly);

        var irisKeyBefore = request.Headers["iris-key"];
        new RebusAdapter().CreateWrappedMessage(request);

        request.Headers.Should().ContainKey("iris-key");
        request.Headers["iris-key"].Should().Be(irisKeyBefore);
        request.Headers.Should().ContainKey(RebusHeaders.MessageId);
    }

    [Fact(DisplayName = "rbs2-senttime is a round-trippable ISO 8601 DateTimeOffset")]
    public void SentTime_IsRoundTripFormat()
    {
        var request = BuildRequest();
        new RebusAdapter().CreateWrappedMessage(request);

        var sentTime = request.Headers[RebusHeaders.SentTime];
        DateTimeOffset.TryParseExact(
            sentTime,
            "O",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind,
            out _).Should().BeTrue();
    }

    [Fact(DisplayName = "Throws when Json is missing")]
    public void Throws_WhenJsonMissing()
    {
        var request = new MessageRequest
        {
            MessageType = SampleType,
            Json = "",
        };

        var act = () => new RebusAdapter().CreateWrappedMessage(request);
        act.Should().Throw<ArgumentException>();
    }
}
