using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Iris.Brokers.Frameworks;
using Iris.Brokers.Models;
using Xunit;

namespace Iris.Brokers.Test.Frameworks;

/// <summary>
/// Snapshot test for the RebusAdapter envelope. Volatile values (GUIDs, timestamps) are
/// scrubbed to placeholder strings before the produced envelope is compared against the
/// committed fixture, so a meaningful change to the envelope shape will fail this test
/// loudly. This is the pattern the roadmap wants applied to the other framework adapters.
/// </summary>
public class RebusAdapterSnapshotTests
{
    private const string SampleJson = "{\"id\":1,\"name\":\"hello\"}";
    private const string SampleType = "MyApp.Messages.Greeting";
    private const string SampleAssembly = "MyApp.Contracts";

    private static readonly Regex GuidPattern = new(
        @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        RegexOptions.Compiled);

    // Matches DateTimeOffset.ToString("O") output, e.g. 2026-04-10T12:34:56.7891234+01:00 or ...Z
    private static readonly Regex Iso8601Pattern = new(
        @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{1,7}([+-]\d{2}:\d{2}|Z)",
        RegexOptions.Compiled);

    [Fact(DisplayName = "Rebus envelope matches committed snapshot")]
    public void Snapshot_MatchesFixture()
    {
        var request = MessageRequest.Create(
            messageType: SampleType,
            json: SampleJson,
            generateIrisHeaders: false,
            messageFullyQualifiedName: SampleType,
            framework: "Rebus",
            messageAssemblyName: SampleAssembly);

        var body = new RebusAdapter().CreateWrappedMessage(request);

        var snapshot = new
        {
            body,
            // SortedDictionary so JSON property order is stable across runs / runtimes.
            headers = new SortedDictionary<string, string>(request.Headers),
        };

        var actual = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
        var scrubbed = Scrub(actual);

        var expectedPath = Path.Combine(
            AppContext.BaseDirectory,
            "Frameworks",
            "Fixtures",
            "rebus-envelope.expected.json");
        var expected = File.ReadAllText(expectedPath);

        // Normalize line endings so the test passes on both LF and CRLF checkouts.
        Normalize(scrubbed).Should().Be(Normalize(expected));
    }

    private static string Scrub(string json)
    {
        json = Iso8601Pattern.Replace(json, "<timestamp>");
        json = GuidPattern.Replace(json, "<guid>");
        return json;
    }

    private static string Normalize(string s) => s.Replace("\r\n", "\n").TrimEnd();
}
