using FluentAssertions;
using Iris.Components.Templates;

namespace Iris.Components.Test.Templates;

public class TemplateFunctionsTests
{
    [Fact(DisplayName = "NewGuid returns a valid GUID string")]
    public void NewGuid_Returns_ValidGuidString()
    {
        var result = TemplateFunctions.Invoke("NewGuid");

        Guid.TryParse(result, out _).Should().BeTrue("result should be a parseable GUID");
    }

    [Fact(DisplayName = "Now returns a valid ISO-8601 DateTimeOffset string")]
    public void Now_Returns_ValidDateTimeOffsetString()
    {
        var result = TemplateFunctions.Invoke("Now");

        DateTimeOffset.TryParse(result, out var parsed).Should().BeTrue("result should be a parseable DateTimeOffset");
        parsed.Should().BeCloseTo(DateTimeOffset.Now, TimeSpan.FromSeconds(5));
    }

    [Fact(DisplayName = "UtcNow returns a valid ISO-8601 UTC DateTimeOffset string")]
    public void UtcNow_Returns_ValidUtcDateTimeOffsetString()
    {
        var result = TemplateFunctions.Invoke("UtcNow");

        DateTimeOffset.TryParse(result, out var parsed).Should().BeTrue("result should be a parseable DateTimeOffset");
        parsed.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact(DisplayName = "RandomInt returns an int within default range")]
    public void RandomInt_Returns_IntWithinDefaultRange()
    {
        var result = TemplateFunctions.Invoke("RandomInt");

        int.TryParse(result, out var value).Should().BeTrue("result should be parseable as int");
        value.Should().BeInRange(0, 999);
    }

    [Fact(DisplayName = "RandomInt respects min and max args")]
    public void RandomInt_Respects_MinMaxArgs()
    {
        var result = TemplateFunctions.Invoke("RandomInt", "5", "10");

        int.TryParse(result, out var value).Should().BeTrue("result should be parseable as int");
        value.Should().BeInRange(5, 9);
    }

    [Fact(DisplayName = "RandomString returns alphanumeric string of default length")]
    public void RandomString_Returns_AlphanumericStringOfDefaultLength()
    {
        var result = TemplateFunctions.Invoke("RandomString");

        result.Should().HaveLength(8);
        result.Should().MatchRegex("^[a-zA-Z0-9]+$", "result should be alphanumeric");
    }

    [Fact(DisplayName = "RandomString returns alphanumeric string of specified length")]
    public void RandomString_Returns_AlphanumericStringOfSpecifiedLength()
    {
        var result = TemplateFunctions.Invoke("RandomString", "16");

        result.Should().HaveLength(16);
        result.Should().MatchRegex("^[a-zA-Z0-9]+$", "result should be alphanumeric");
    }

    [Fact(DisplayName = "Unknown function returns original expression placeholder")]
    public void UnknownFunction_Returns_OriginalExpression()
    {
        var result = TemplateFunctions.Invoke("NotARealFunction");

        result.Should().Be("{{NotARealFunction()}}");
    }

    [Fact(DisplayName = "GetAvailableFunctions returns all expected registered functions")]
    public void GetAvailableFunctions_Returns_AllExpectedFunctions()
    {
        var functions = TemplateFunctions.GetAvailableFunctions();

        functions.Should().ContainKey("NewGuid");
        functions.Should().ContainKey("Now");
        functions.Should().ContainKey("UtcNow");
        functions.Should().ContainKey("RandomInt");
        functions.Should().ContainKey("RandomString");
    }

    [Fact(DisplayName = "Increment is NOT in available functions")]
    public void Increment_Is_NotInAvailableFunctions()
    {
        var functions = TemplateFunctions.GetAvailableFunctions();

        functions.Should().NotContainKey("Increment");
    }

    [Fact(DisplayName = "GetCompatibleFunctions filters by Guid type")]
    public void GetCompatibleFunctions_FiltersBy_GuidType()
    {
        var functions = TemplateFunctions.GetCompatibleFunctions("Guid");

        functions.Should().ContainKey("NewGuid");
        functions.Should().NotContainKey("RandomInt");
        functions.Should().NotContainKey("RandomString");
    }

    [Fact(DisplayName = "GetCompatibleFunctions filters by int type")]
    public void GetCompatibleFunctions_FiltersBy_IntType()
    {
        var functions = TemplateFunctions.GetCompatibleFunctions("int");

        functions.Should().ContainKey("RandomInt");
        functions.Should().NotContainKey("NewGuid");
    }

    [Fact(DisplayName = "GetCompatibleFunctions filters by string type includes all functions")]
    public void GetCompatibleFunctions_FiltersBy_StringType_IncludesAll()
    {
        var functions = TemplateFunctions.GetCompatibleFunctions("string");

        functions.Should().ContainKey("NewGuid");
        functions.Should().ContainKey("Now");
        functions.Should().ContainKey("UtcNow");
        functions.Should().ContainKey("RandomInt");
        functions.Should().ContainKey("RandomString");
    }

    [Fact(DisplayName = "GetCompatibleFunctions filters by DateTime type")]
    public void GetCompatibleFunctions_FiltersBy_DateTimeType()
    {
        var functions = TemplateFunctions.GetCompatibleFunctions("DateTime");

        functions.Should().ContainKey("Now");
        functions.Should().ContainKey("UtcNow");
        functions.Should().NotContainKey("NewGuid");
        functions.Should().NotContainKey("RandomInt");
        functions.Should().NotContainKey("RandomString");
    }

    [Fact(DisplayName = "GetCompatibleFunctions returns empty for unknown type")]
    public void GetCompatibleFunctions_ReturnsEmpty_ForUnknownType()
    {
        var functions = TemplateFunctions.GetCompatibleFunctions("SomeUnknownType");

        functions.Should().BeEmpty();
    }

    [Fact(DisplayName = "GetAvailableFunctions returns TemplateFunction records with correct metadata")]
    public void GetAvailableFunctions_Returns_CorrectMetadata()
    {
        var functions = TemplateFunctions.GetAvailableFunctions();

        var newGuid = functions["NewGuid"];
        newGuid.Name.Should().Be("NewGuid");
        newGuid.Description.Should().NotBeNullOrWhiteSpace();
        newGuid.CompatibleTypes.Should().Contain("Guid");
        newGuid.CompatibleTypes.Should().Contain("string");
    }
}
