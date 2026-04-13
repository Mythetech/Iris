using FluentAssertions;
using Iris.Components.Templates;

namespace Iris.Components.Test.Templates;

public class TemplateResolverTests
{
    private readonly ITemplateResolver _resolver = new TemplateResolver();

    [Fact(DisplayName = "Resolves {{NewGuid()}} to a valid GUID string")]
    public async Task ResolveAsync_NewGuid_ReturnsValidGuid()
    {
        var json = """{"id": "{{NewGuid()}}"}""";

        var result = await _resolver.ResolveAsync(json);

        var doc = System.Text.Json.JsonDocument.Parse(result);
        var idValue = doc.RootElement.GetProperty("id").GetString();
        Guid.TryParse(idValue, out _).Should().BeTrue("resolved value should be a parseable GUID");
    }

    [Fact(DisplayName = "Resolves {{UtcNow()}} to a valid DateTimeOffset string")]
    public async Task ResolveAsync_UtcNow_ReturnsValidDateTimeOffset()
    {
        var json = """{"timestamp": "{{UtcNow()}}"}""";

        var result = await _resolver.ResolveAsync(json);

        var doc = System.Text.Json.JsonDocument.Parse(result);
        var tsValue = doc.RootElement.GetProperty("timestamp").GetString();
        DateTimeOffset.TryParse(tsValue, out var parsed).Should().BeTrue("resolved value should be a parseable DateTimeOffset");
        parsed.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact(DisplayName = "Resolves {{RandomInt(1, 50)}} to an integer within the specified range")]
    public async Task ResolveAsync_RandomInt_WithArgs_ReturnsValueInRange()
    {
        var json = """{"count": "{{RandomInt(1, 50)}}"}""";

        var result = await _resolver.ResolveAsync(json);

        var doc = System.Text.Json.JsonDocument.Parse(result);
        var countValue = doc.RootElement.GetProperty("count").GetString();
        int.TryParse(countValue, out var value).Should().BeTrue("resolved value should be parseable as int");
        value.Should().BeInRange(1, 49);
    }

    [Fact(DisplayName = "Leaves static string values unchanged")]
    public async Task ResolveAsync_StaticValue_IsUnchanged()
    {
        var json = """{"name": "hello world", "active": true, "count": 42}""";

        var result = await _resolver.ResolveAsync(json);

        var doc = System.Text.Json.JsonDocument.Parse(result);
        doc.RootElement.GetProperty("name").GetString().Should().Be("hello world");
        doc.RootElement.GetProperty("active").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("count").GetInt32().Should().Be(42);
    }

    [Fact(DisplayName = "Handles nested objects with expressions")]
    public async Task ResolveAsync_NestedObject_ResolvesExpressions()
    {
        var json = """
            {
                "outer": {
                    "inner": {
                        "id": "{{NewGuid()}}"
                    }
                }
            }
            """;

        var result = await _resolver.ResolveAsync(json);

        var doc = System.Text.Json.JsonDocument.Parse(result);
        var idValue = doc.RootElement
            .GetProperty("outer")
            .GetProperty("inner")
            .GetProperty("id")
            .GetString();

        Guid.TryParse(idValue, out _).Should().BeTrue("nested expression should resolve to a valid GUID");
    }

    [Fact(DisplayName = "Handles arrays with expressions")]
    public async Task ResolveAsync_Array_ResolvesExpressions()
    {
        var json = """{"ids": ["{{NewGuid()}}", "{{NewGuid()}}", "static-value"]}""";

        var result = await _resolver.ResolveAsync(json);

        var doc = System.Text.Json.JsonDocument.Parse(result);
        var ids = doc.RootElement.GetProperty("ids");
        ids.GetArrayLength().Should().Be(3);
        Guid.TryParse(ids[0].GetString(), out _).Should().BeTrue("first array element should resolve to a GUID");
        Guid.TryParse(ids[1].GetString(), out _).Should().BeTrue("second array element should resolve to a GUID");
        ids[2].GetString().Should().Be("static-value");
    }

    [Fact(DisplayName = "Generates unique values per expression (two NewGuid calls get different values)")]
    public async Task ResolveAsync_TwoNewGuidExpressions_ProduceDifferentValues()
    {
        var json = """{"id1": "{{NewGuid()}}", "id2": "{{NewGuid()}}"}""";

        var result = await _resolver.ResolveAsync(json);

        var doc = System.Text.Json.JsonDocument.Parse(result);
        var id1 = doc.RootElement.GetProperty("id1").GetString();
        var id2 = doc.RootElement.GetProperty("id2").GetString();

        id1.Should().NotBe(id2, "each {{NewGuid()}} expression should produce a unique value");
    }

    [Fact(DisplayName = "Returns original string for invalid JSON")]
    public async Task ResolveAsync_InvalidJson_ReturnsOriginalString()
    {
        var notJson = "this is not json { at all {{NewGuid()}}";

        var result = await _resolver.ResolveAsync(notJson);

        result.Should().Be(notJson);
    }

    [Fact(DisplayName = "Handles empty JSON object")]
    public async Task ResolveAsync_EmptyObject_ReturnsValidJson()
    {
        var json = "{}";

        var result = await _resolver.ResolveAsync(json);

        var doc = System.Text.Json.JsonDocument.Parse(result);
        doc.RootElement.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
    }

    [Fact(DisplayName = "Handles null JSON input gracefully")]
    public async Task ResolveAsync_NullInput_ReturnsOriginalString()
    {
        var result = await _resolver.ResolveAsync(null!);

        result.Should().BeNull();
    }
}
