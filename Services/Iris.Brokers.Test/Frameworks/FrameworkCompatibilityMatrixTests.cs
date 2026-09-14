using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Iris.Brokers;
using Iris.Brokers.Frameworks;
using Xunit;

namespace Iris.Brokers.Test.Frameworks;

/// <summary>
/// Every framework against every broker, at four user-header counts, as one committed
/// fixture.
///
/// <para>
/// <see cref="FrameworkCompatibilityTests"/> covers fifteen combinations chosen because each
/// demonstrates a specific rule, and each carries the explanation of that rule. This covers
/// all ninety-six, which is the half those cannot: the combinations nobody thought to write
/// a case for. A change to a key declaration, a carrier limit or a reason string shows up
/// here as an exact diff of which cells moved.
/// </para>
///
/// <para>
/// The answer is user-facing. Supported decides whether the send button works, the reason is
/// printed verbatim in the framework picker, and the dropped keys are listed to the user as
/// what the broker will not carry.
/// </para>
/// </summary>
public class FrameworkCompatibilityMatrixTests
{
    /// <summary>
    /// Zero is the base case. One and two show optional keys being dropped to fit, one at a
    /// time, which is the only place declaration order is observable. Ten is past every
    /// carrier's limit, so it is where a required key fails rather than being dropped.
    /// </summary>
    private static readonly int[] UserHeaderCounts = [0, 1, 2, 10];

    private static IFramework[] Adapters() =>
    [
        new BrighterAdapter(), new EasyNetQAdapter(), new MassTransitAdapter(),
        new NServiceBusAdapter(), new RebusAdapter(), new WolverineAdapter(),
    ];

    /// <summary>
    /// Labelled by transport rather than by <c>IConnection.Name</c>, which for RabbitMQ is a
    /// deployment label the connection derives from its own address. The provider name is
    /// passed in because it is interpolated into every failure message, and the fixture is a
    /// record of what the user is actually shown.
    /// </summary>
    private static (string Transport, IConnection Connection)[] Connections() =>
    [
        (ConnectorTransports.RabbitMq, BrokerSenderInterfaceTests.Rabbit(ConnectorProviders.RabbitMq)),
        (ConnectorTransports.AzureServiceBus, BrokerSenderInterfaceTests.ServiceBus(ConnectorProviders.Azure)),
        (ConnectorTransports.AzureQueueStorage, BrokerSenderInterfaceTests.QueueStorage(ConnectorProviders.Azure)),
        (ConnectorTransports.SimpleQueueService, BrokerSenderInterfaceTests.Sqs(ConnectorProviders.Amazon)),
    ];

    [Fact(DisplayName = "The whole compatibility matrix matches the committed snapshot")]
    public void Matrix_matches_the_fixture()
    {
        // A JsonObject rather than a dictionary: it keeps insertion order, and the loops
        // below are already fully determined, so the fixture groups by broker and counts
        // upwards instead of sorting 10 in front of 2.
        var matrix = new JsonObject();

        foreach (var (transport, connection) in Connections())
        foreach (var adapter in Adapters())
        foreach (var userHeaderCount in UserHeaderCounts)
        {
            var result = FrameworkCompatibility.Check(adapter, connection, userHeaderCount);
            var header = userHeaderCount == 1 ? "header" : "headers";
            matrix[$"{adapter.Name} on {transport} with {userHeaderCount} user {header}"] = Describe(result);
        }

        var actual = matrix.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });

        var expectedPath = Path.Combine(
            AppContext.BaseDirectory, "Frameworks", "Fixtures", "compatibility-matrix.expected.json");
        var expected = File.ReadAllText(expectedPath);

        Normalize(actual).Should().Be(Normalize(expected));
    }

    [Fact(DisplayName = "Every adapter and every broker is in the matrix")]
    public void The_matrix_is_complete()
    {
        // The fixture is only a drift guard for what it covers, and a new adapter or a new
        // broker that nobody added here would pass it by being absent.
        var cells = Connections().Length * Adapters().Length * UserHeaderCounts.Length;

        var expectedPath = Path.Combine(
            AppContext.BaseDirectory, "Frameworks", "Fixtures", "compatibility-matrix.expected.json");
        var fixture = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(expectedPath))!;

        fixture.Should().HaveCount(cells);
        Adapters().Should().HaveCount(6, "six adapters is the shipped set");
        Connections().Should().HaveCount(4, "four brokers is the shipped set");
    }

    private static string Describe(CompatibilityResult result)
    {
        if (!result.Supported)
            return $"rejected: {result.Reason}";

        var headers = result.DroppedKeys.Where(k => k.Location == KeyLocation.Header).Select(k => k.Name).ToList();
        var properties = result.DroppedKeys.Where(k => k.Location == KeyLocation.TransportProperty)
            .Select(k => k.Property!.Value.ToString()).ToList();

        // Declaration order, which is the order the drop-to-fit loop walks backwards through,
        // so it is the part of the answer that would silently change if the keys were
        // reordered in an adapter.
        var parts = new List<string>();
        if (headers.Count > 0)
            parts.Add($"dropped headers: {string.Join(", ", headers)}");
        if (properties.Count > 0)
            parts.Add($"dropped properties: {string.Join(", ", properties)}");

        return parts.Count == 0 ? "supported" : $"supported; {string.Join("; ", parts)}";
    }

    private static string Normalize(string s) => s.Replace("\r\n", "\n").TrimEnd();
}
