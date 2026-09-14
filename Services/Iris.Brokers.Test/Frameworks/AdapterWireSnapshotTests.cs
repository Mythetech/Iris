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
/// Snapshots of everything the four remaining adapters put on the wire, following the
/// pattern <see cref="RebusAdapterSnapshotTests"/> established and
/// <see cref="MassTransitAdapterSnapshotTests"/> repeated.
///
/// <para>
/// Those two capture the body, because that is where MassTransit and Rebus write. These four
/// write to three places: the body, <c>request.Headers</c>, and
/// <c>request.TransportProperties</c>, which carriers lift to native AMQP or Service Bus
/// properties. All three are captured here. Wolverine and EasyNetQ return the body
/// unchanged and say everything in the other two bags, so a snapshot of the body alone
/// would have pinned nothing at all about them.
/// </para>
///
/// <para>
/// The point of the fixtures is that changing what a consumer sees has to be a deliberate
/// edit to a committed file, where a reviewer reads it. A wrong urn spelling shipped once
/// because no such file existed.
/// </para>
/// </summary>
public class AdapterWireSnapshotTests
{
    private const string SampleJson = "{\"id\":1,\"name\":\"hello\"}";
    private const string SampleType = "MyApp.Messages.Greeting";
    private const string SampleAssembly = "MyApp.Contracts";

    private static readonly Regex GuidPattern = new(
        @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        RegexOptions.Compiled);

    private static readonly Regex Iso8601Pattern = new(
        @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{1,7}([+-]\d{2}:\d{2}|Z)",
        RegexOptions.Compiled);

    // NServiceBus and Wolverine both stamp a time in their own non-ISO format:
    // "yyyy-MM-dd HH:mm:ss:ffffff Z".
    private static readonly Regex NServiceBusTimePattern = new(
        @"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}:\d{6} Z",
        RegexOptions.Compiled);

    // The machine name, and the NServiceBus package version, which NServiceBusAdapterTests
    // already pins to the referenced assembly. Leaving the version in would turn every
    // routine package bump into a fixture edit that proves nothing.
    private static readonly Regex MachineNamePattern = new(
        @"(?<=""NServiceBus\.OriginatingMachine"": "")[^""]*",
        RegexOptions.Compiled);

    private static readonly Regex NServiceBusVersionPattern = new(
        @"(?<=""NServiceBus\.Version"": "")[^""]*",
        RegexOptions.Compiled);

    public static TheoryData<string> Adapters() => new("NServiceBus", "Brighter", "Wolverine", "EasyNetQ");

    private static IFramework Adapter(string name) => name switch
    {
        "NServiceBus" => new NServiceBusAdapter(),
        "Brighter" => new BrighterAdapter(),
        "Wolverine" => new WolverineAdapter(),
        "EasyNetQ" => new EasyNetQAdapter(),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "No adapter by that name."),
    };

    [Theory(DisplayName = "The adapter's body, headers and transport properties match the committed snapshot")]
    [MemberData(nameof(Adapters))]
    public void Wire_matches_the_fixture(string adapterName)
    {
        var adapter = Adapter(adapterName);
        var request = MessageRequest.Create(
            messageType: "Greeting",
            json: SampleJson,
            generateIrisHeaders: false,
            messageFullyQualifiedName: SampleType,
            framework: adapterName,
            messageAssemblyName: SampleAssembly);

        request.WrapMessage(adapter);

        var actual = Serialize(request);

        var expectedPath = Path.Combine(
            AppContext.BaseDirectory, "Frameworks", "Fixtures", $"{adapterName.ToLowerInvariant()}-wire.expected.json");
        var expected = File.ReadAllText(expectedPath);

        Normalize(Scrub(actual)).Should().Be(Normalize(expected));
    }

    /// <summary>
    /// The body goes in parsed where it is JSON, so a formatting change is not a diff and a
    /// structural one is. Wolverine, EasyNetQ and Brighter return the caller's JSON
    /// untouched; NServiceBus returns its own envelope.
    /// </summary>
    private static string Serialize(MessageRequest request)
    {
        var snapshot = new JsonObject
        {
            // SortedDictionary ordering: property order has to be stable across runtimes.
            ["headers"] = new JsonObject(new SortedDictionary<string, string>(request.Headers)
                .Select(h => KeyValuePair.Create(h.Key, (JsonNode?)JsonValue.Create(h.Value)))),
            ["transportProperties"] = TransportPropertiesOf(request),
            ["body"] = JsonNode.Parse(request.Json),
        };

        return snapshot.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }

    /// <summary>
    /// Only the properties actually set, so a fixture entry appearing or disappearing is the
    /// signal. A carrier writes nothing for an unset one.
    /// </summary>
    private static JsonObject TransportPropertiesOf(MessageRequest request)
    {
        var properties = request.TransportProperties;
        var set = new JsonObject();

        foreach (var property in properties.SetProperties())
        {
            set[property.ToString()] = property switch
            {
                TransportProperty.MessageId => JsonValue.Create(properties.MessageId),
                TransportProperty.CorrelationId => JsonValue.Create(properties.CorrelationId),
                TransportProperty.ContentType => JsonValue.Create(properties.ContentType),
                TransportProperty.Type => JsonValue.Create(properties.Type),
                TransportProperty.Timestamp => JsonValue.Create(properties.Timestamp!.Value.ToString("O")),
                TransportProperty.Persistent => JsonValue.Create(properties.Persistent!.Value),
                _ => throw new ArgumentOutOfRangeException(nameof(request), property, "Unmapped transport property."),
            };
        }

        return set;
    }

    private static string Scrub(string json)
    {
        json = Iso8601Pattern.Replace(json, "<timestamp>");
        json = NServiceBusTimePattern.Replace(json, "<timestamp>");
        json = MachineNamePattern.Replace(json, "<machine>");
        json = NServiceBusVersionPattern.Replace(json, "<nsb-version>");
        return GuidPattern.Replace(json, "<guid>");
    }

    private static string Normalize(string s) => s.Replace("\r\n", "\n").TrimEnd();
}
