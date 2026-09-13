using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FluentAssertions;
using Iris.Brokers.Frameworks;
using Iris.Brokers.Models;
using Xunit;

namespace Iris.Brokers.Test.Frameworks;

/// <summary>
/// Snapshot test for the MassTransitAdapter envelope, the same pattern as
/// <see cref="RebusAdapterSnapshotTests"/>. Volatile values (GUIDs, timestamps, and every Host
/// member, which reports the machine and process running the test) are scrubbed before the
/// produced envelope is compared against the committed fixture. The absence of a snapshot here
/// is what let a wrong urn spelling ship, so the fixture is the point: a change to the wire
/// shape has to be made deliberately, in the fixture, where a reviewer can see it.
/// </summary>
public class MassTransitAdapterSnapshotTests
{
    private const string SampleJson = "{\"id\":1,\"name\":\"hello\"}";
    private const string SampleType = "MyApp.Messages.Greeting";

    private static readonly Regex GuidPattern = new(
        @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        RegexOptions.Compiled);

    private static readonly Regex Iso8601Pattern = new(
        @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?([+-]\d{2}:\d{2}|Z)?",
        RegexOptions.Compiled);

    [Fact(DisplayName = "MassTransit envelope matches committed snapshot")]
    public void Snapshot_MatchesFixture()
    {
        var request = MessageRequest.Create(
            messageType: "Greeting",
            json: SampleJson,
            generateIrisHeaders: false,
            messageFullyQualifiedName: SampleType,
            framework: "MassTransit");

        var body = new MassTransitAdapter().CreateWrappedMessage(request);

        var node = JsonNode.Parse(body)!.AsObject();
        ScrubHost(node);

        var actual = node.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });

        var expectedPath = Path.Combine(
            AppContext.BaseDirectory,
            "Frameworks",
            "Fixtures",
            "masstransit-envelope.expected.json");
        var expected = File.ReadAllText(expectedPath);

        Normalize(Scrub(actual)).Should().Be(Normalize(expected));
    }

    /// <summary>
    /// Every Host value is machine, process or runtime specific, so none of them can be pinned.
    /// The member names still can be, and those are the part a consumer reads.
    /// </summary>
    private static void ScrubHost(JsonObject envelope)
    {
        var host = envelope["Host"]!.AsObject();
        foreach (var name in host.Select(p => p.Key).ToArray())
        {
            host[name] = "<host>";
        }
    }

    private static string Scrub(string json)
    {
        json = Iso8601Pattern.Replace(json, "<timestamp>");
        return GuidPattern.Replace(json, "<guid>");
    }

    private static string Normalize(string s) => s.Replace("\r\n", "\n").TrimEnd();
}
