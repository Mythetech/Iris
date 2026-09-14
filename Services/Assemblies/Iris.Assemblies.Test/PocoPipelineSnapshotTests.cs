using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Iris.Assemblies;
using Iris.Contracts.Assemblies.Models;
using Xunit;

namespace Iris.Api.Test.Assemblies;

public record LineItem(string Sku, int Quantity, decimal UnitPrice);

public record ShippingAddress(string Street, string City, string PostCode, string CountryCode);

public enum FulfilmentChannel { Warehouse, DropShip, Digital }

/// <summary>
/// One contract type carried end to end: reflected into <c>TypeData</c> by
/// <see cref="TypeMapper"/>, then turned into the editor's starting JSON by
/// <see cref="SampleJsonGenerator"/>.
/// </summary>
public record OrderPlaced(
    Guid OrderId,
    string CustomerName,
    DateTime PlacedAt,
    bool IsPriority,
    int? DiscountPercent,
    FulfilmentChannel Channel,
    ShippingAddress ShipTo,
    List<LineItem> Lines,
    Dictionary<string, string> Metadata);

/// <summary>
/// The POCO pipeline, snapshotted at both ends.
///
/// <para>
/// This is the pipeline a user actually exercises when they pick a type out of a loaded
/// package: the mapped shape drives the template form builder, and the sample JSON is what
/// lands in the editor. <see cref="TypeMapperTests"/> and <see cref="SampleJsonGeneratorTests"/>
/// each check one rule at a time against a purpose-built type. This runs one realistic
/// contract, with a nested record, a collection, a dictionary, an enum and a nullable, all
/// the way through, so a change in how any of those compose shows up as an exact diff rather
/// than as a form that renders slightly wrong.
/// </para>
/// </summary>
public class PocoPipelineSnapshotTests
{
    [Fact(DisplayName = "A realistic contract type maps and generates a sample matching the committed snapshot")]
    public void Pipeline_matches_the_fixture()
    {
        var typeData = typeof(OrderPlaced).ToContract();
        var sample = new SampleJsonGenerator().GenerateSample(typeData);

        var actual = JsonSerializer.Serialize(new
        {
            mapped = typeData,
            sample,
        }, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            // Named rather than numbered, so the fixture is readable. The ordinals are what
            // actually travel, and Type_kinds_keep_their_ordinals covers those.
            Converters = { new JsonStringEnumConverter() },
        });

        var expectedPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "poco-pipeline.expected.json");
        var expected = File.ReadAllText(expectedPath);

        Normalize(Scrub(actual)).Should().Be(Normalize(expected));
    }

    [Fact(DisplayName = "TypeKind members keep their ordinals")]
    public void Type_kinds_keep_their_ordinals()
    {
        // TypeKind is serialized by number wherever TypeData crosses the contract boundary,
        // so inserting a member anywhere but the end relabels every property already
        // described by an older value. Nothing throws; the form builder just renders the
        // wrong editor.
        ((int)TypeKind.Primitive).Should().Be(0);
        ((int)TypeKind.Complex).Should().Be(1);
        ((int)TypeKind.Enum).Should().Be(2);
        ((int)TypeKind.Collection).Should().Be(3);
        ((int)TypeKind.Dictionary).Should().Be(4);
        Enum.GetValues<TypeKind>().Should().HaveCount(5, "a new kind goes on the end");
    }

    /// <summary>
    /// The generated sample stamps a fresh Guid and the current time, which are the only two
    /// values in it that are not fixed by the type.
    /// </summary>
    private static string Scrub(string json)
    {
        json = System.Text.RegularExpressions.Regex.Replace(
            json, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", "<guid>");
        return System.Text.RegularExpressions.Regex.Replace(
            json, @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?([+-]\d{2}:\d{2}|Z)?", "<timestamp>");
    }

    private static string Normalize(string s) => s.Replace("\r\n", "\n").TrimEnd();
}
