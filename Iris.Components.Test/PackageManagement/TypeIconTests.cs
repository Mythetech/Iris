using FluentAssertions;
using Iris.Components.PackageManagement;
using Iris.Components.Theme;
using Xunit;

namespace Iris.Components.Test.PackageManagement;

public class TypeIconTests : IrisTestContext
{
    [Theory(DisplayName = "Displays correct type icon for strings")]
    [InlineData("String")]
    public void Displays_CorrectlyTyped_StringIcon(string type)
    {
        var cut = RenderComponent<TypeIcon>(parameters => parameters
            .Add(p => p.Type, type));

        cut.Markup.Should().Contain(IrisIcons.Types.String);
    }

    [Theory(DisplayName = "Displays correct type icon for numbers")]
    [InlineData("Number")]
    [InlineData("Single")]
    [InlineData("Double")]
    [InlineData("Float")]
    [InlineData("Decimal")]
    [InlineData("Int16")]
    [InlineData("Int32")]
    [InlineData("Int64")]
    public void Displays_CorrectlyTyped_NumberIcon(string type)
    {
        var cut = RenderComponent<TypeIcon>(parameters => parameters
            .Add(p => p.Type, type));

        cut.Markup.Should().Contain(IrisIcons.Types.Number);
    }

    [Theory(DisplayName = "Displays correct type icon for dates")]
    [InlineData("Date")]
    [InlineData("DateTime")]
    [InlineData("DateOnly")]
    [InlineData("DateTimeOffset")]
    public void Displays_CorrectlyTyped_DateIcon(string type)
    {
        var cut = RenderComponent<TypeIcon>(parameters => parameters
            .Add(p => p.Type, type));

        cut.Markup.Should().Contain(IrisIcons.Types.Date);
    }

    [Theory(DisplayName = "Displays correct type icon for booleans")]
    [InlineData("Boolean")]
    public void Displays_CorrectlyTyped_BooleanIcon(string type)
    {
        var cut = RenderComponent<TypeIcon>(parameters => parameters
            .Add(p => p.Type, type));

        cut.Markup.Should().Contain(IrisIcons.Types.Boolean);
    }

    [Theory(DisplayName = "Displays correct type icon for objects")]
    [InlineData("Object")]
    [InlineData("UnrecognizedType")]
    public void Displays_CorrectlyTyped_ObjectIcon(string type)
    {
        var cut = RenderComponent<TypeIcon>(parameters => parameters
            .Add(p => p.Type, type));

        cut.Markup.Should().Contain(IrisIcons.Types.Object);
    }

    [Theory(DisplayName = "Displays array icon for types ending in '[]'")]
    [InlineData("SomeType[]")]
    public void Displays_CorrectlyTyped_ArrayIcon(string type)
    {
        var cut = RenderComponent<TypeIcon>(parameters => parameters
            .Add(p => p.Type, type));

        cut.Markup.Should().Contain(IrisIcons.Types.Array);
    }
    
    [Theory(DisplayName = "Displays key icon for key/secret types")]
    [InlineData("Guid")]
    public void Displays_CorrectlyTyped_KeyIcon(string type)
    {
        var cut = RenderComponent<TypeIcon>(parameters => parameters
            .Add(p => p.Type, type));

        cut.Markup.Should().Contain(IrisIcons.Types.Key);
    }

    [Fact(DisplayName = "Displays default icon for null or empty type")]
    public void Displays_DefaultIcon_For_NullOrEmptyType()
    {
        var cut = RenderComponent<TypeIcon>(parameters => parameters
            .Add(p => p.Type, null));

        cut.Markup.Should().Contain(IrisIcons.Types.None);
    }
}