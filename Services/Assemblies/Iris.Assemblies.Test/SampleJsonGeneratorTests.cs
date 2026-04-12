using System.Text.Json.Nodes;
using FluentAssertions;
using Iris.Assemblies;
using Iris.Contracts.Assemblies.Models;
using Xunit;

namespace Iris.Api.Test.Assemblies;

public class SampleJsonGeneratorTests
{
    private readonly SampleJsonGenerator _generator = new();

    private static TypeData MakeTypeData(params PropertyData[] properties) => new()
    {
        Name = "TestType",
        FullyQualifiedName = "Test.TestType",
        Properties = properties.ToList()
    };

    [Fact(DisplayName = "Generates string default")]
    public void Generates_String_Default()
    {
        var typeData = MakeTypeData(new PropertyData { Name = "Name", TypeName = "String", Kind = TypeKind.Primitive });
        var json = _generator.GenerateSample(typeData);
        json.AsObject()["Name"]!.GetValue<string>().Should().Be("sample-string");
    }

    [Fact(DisplayName = "Generates Guid as valid GUID string")]
    public void Generates_Guid_Default()
    {
        var typeData = MakeTypeData(new PropertyData { Name = "Id", TypeName = "Guid", Kind = TypeKind.Primitive });
        var json = _generator.GenerateSample(typeData);
        Guid.TryParse(json.AsObject()["Id"]!.GetValue<string>(), out _).Should().BeTrue();
    }

    [Fact(DisplayName = "Generates DateTime as ISO 8601 string")]
    public void Generates_DateTime_Default()
    {
        var typeData = MakeTypeData(new PropertyData { Name = "CreatedAt", TypeName = "DateTime", Kind = TypeKind.Primitive });
        var json = _generator.GenerateSample(typeData);
        DateTime.TryParse(json.AsObject()["CreatedAt"]!.GetValue<string>(), out _).Should().BeTrue();
    }

    [Fact(DisplayName = "Generates integer defaults")]
    public void Generates_Integer_Default()
    {
        var typeData = MakeTypeData(new PropertyData { Name = "Count", TypeName = "Int32", Kind = TypeKind.Primitive });
        var json = _generator.GenerateSample(typeData);
        json.AsObject()["Count"]!.GetValue<int>().Should().Be(1);
    }

    [Fact(DisplayName = "Generates boolean default as true")]
    public void Generates_Boolean_Default()
    {
        var typeData = MakeTypeData(new PropertyData { Name = "IsActive", TypeName = "Boolean", Kind = TypeKind.Primitive });
        var json = _generator.GenerateSample(typeData);
        json.AsObject()["IsActive"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact(DisplayName = "Generates enum with first value")]
    public void Generates_Enum_First_Value()
    {
        var typeData = MakeTypeData(new PropertyData
        {
            Name = "Status", TypeName = "OrderStatus", Kind = TypeKind.Enum,
            EnumValues = new List<string> { "Pending", "Shipped", "Delivered" }
        });
        var json = _generator.GenerateSample(typeData);
        json.AsObject()["Status"]!.GetValue<string>().Should().Be("Pending");
    }

    [Fact(DisplayName = "Generates collection with single element")]
    public void Generates_Collection_With_Element()
    {
        var typeData = MakeTypeData(new PropertyData
        {
            Name = "Tags", TypeName = "List<String>", Kind = TypeKind.Collection,
            GenericArguments = new List<string> { "String" },
            Children = new List<PropertyData> { new() { Name = "[element]", TypeName = "String", Kind = TypeKind.Primitive } }
        });
        var json = _generator.GenerateSample(typeData);
        var arr = json.AsObject()["Tags"]!.AsArray();
        arr.Should().HaveCount(1);
        arr[0]!.GetValue<string>().Should().Be("sample-string");
    }

    [Fact(DisplayName = "Generates dictionary with single entry")]
    public void Generates_Dictionary_With_Entry()
    {
        var typeData = MakeTypeData(new PropertyData
        {
            Name = "Metadata", TypeName = "Dictionary<String, Int32>", Kind = TypeKind.Dictionary,
            GenericArguments = new List<string> { "String", "Int32" },
            Children = new List<PropertyData>
            {
                new() { Name = "[key]", TypeName = "String", Kind = TypeKind.Primitive },
                new() { Name = "[value]", TypeName = "Int32", Kind = TypeKind.Primitive }
            }
        });
        var json = _generator.GenerateSample(typeData);
        var dict = json.AsObject()["Metadata"]!.AsObject();
        dict.Count.Should().Be(1);
        dict["key"]!.GetValue<int>().Should().Be(1);
    }

    [Fact(DisplayName = "Generates nested complex objects")]
    public void Generates_Nested_Complex()
    {
        var typeData = MakeTypeData(new PropertyData
        {
            Name = "Address", TypeName = "Address", Kind = TypeKind.Complex,
            Children = new List<PropertyData>
            {
                new() { Name = "Street", TypeName = "String", Kind = TypeKind.Primitive },
                new() { Name = "City", TypeName = "String", Kind = TypeKind.Primitive },
            }
        });
        var json = _generator.GenerateSample(typeData);
        var addr = json.AsObject()["Address"]!.AsObject();
        addr["Street"]!.GetValue<string>().Should().Be("sample-string");
        addr["City"]!.GetValue<string>().Should().Be("sample-string");
    }

    [Fact(DisplayName = "Generates empty object for depth-limited placeholders")]
    public void Generates_Empty_Object_For_Placeholder()
    {
        var typeData = MakeTypeData(new PropertyData
        {
            Name = "Nested", TypeName = "SomeType", Kind = TypeKind.Complex, Children = null
        });
        var json = _generator.GenerateSample(typeData);
        json.AsObject()["Nested"]!.AsObject().Count.Should().Be(0);
    }
}
