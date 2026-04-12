using FluentAssertions;
using Iris.Assemblies;
using Iris.Contracts.Assemblies.Models;
using Xunit;

namespace Iris.Api.Test.Assemblies;

public enum TestStatus { Active, Inactive, Pending }

public record SimpleTestType(string Name, int Amount, bool IsActive, Guid Id, DateTime CreatedAt);
public record NullableTestType(int? Count, DateTime? Timestamp, Guid? Reference);
public record EnumTestType(TestStatus Status, string Label);
public record Address(string Street, string City, string Zip);
public record Customer(string Name, Address BillingAddress);
public record OrderWithList(string OrderId, List<string> Tags, int[] Quantities);
public record OrderWithDict(string OrderId, Dictionary<string, int> Metadata);
public record NestedComplex(string Name, Customer Customer);

public class CyclicA
{
    public string Name { get; set; } = "";
    public CyclicB? Related { get; set; }
}

public class CyclicB
{
    public string Label { get; set; } = "";
    public CyclicA? Back { get; set; }
}

public record DeeplyNested(string Value, Level2 Child);
public record Level2(string Value, Level3 Child);
public record Level3(string Value, Level4 Child);
public record Level4(string Value, string Leaf);

public class TypeMapperTests
{
    [Fact(DisplayName = "Maps primitive properties with correct TypeKind")]
    public void Maps_Primitive_Properties()
    {
        var result = typeof(SimpleTestType).ToContract();

        result.Name.Should().Be("SimpleTestType");
        result.FullyQualifiedName.Should().Be(typeof(SimpleTestType).FullName);
        result.Properties.Should().HaveCount(5);

        var nameProp = result.Properties!.First(p => p.Name == "Name");
        nameProp.TypeName.Should().Be("String");
        nameProp.Kind.Should().Be(TypeKind.Primitive);
        nameProp.IsNullable.Should().BeFalse();
        nameProp.Children.Should().BeNull();

        var idProp = result.Properties!.First(p => p.Name == "Id");
        idProp.TypeName.Should().Be("Guid");
        idProp.Kind.Should().Be(TypeKind.Primitive);

        var dateProp = result.Properties!.First(p => p.Name == "CreatedAt");
        dateProp.TypeName.Should().Be("DateTime");
        dateProp.Kind.Should().Be(TypeKind.Primitive);
    }

    [Fact(DisplayName = "Unwraps nullable types and sets IsNullable")]
    public void Unwraps_Nullable_Types()
    {
        var result = typeof(NullableTestType).ToContract();
        result.Properties.Should().HaveCount(3);

        var countProp = result.Properties!.First(p => p.Name == "Count");
        countProp.TypeName.Should().Be("Int32");
        countProp.Kind.Should().Be(TypeKind.Primitive);
        countProp.IsNullable.Should().BeTrue();

        var timestampProp = result.Properties!.First(p => p.Name == "Timestamp");
        timestampProp.TypeName.Should().Be("DateTime");
        timestampProp.IsNullable.Should().BeTrue();
    }

    [Fact(DisplayName = "Maps enum properties with value list")]
    public void Maps_Enum_Properties()
    {
        var result = typeof(EnumTestType).ToContract();

        var statusProp = result.Properties!.First(p => p.Name == "Status");
        statusProp.TypeName.Should().Be("TestStatus");
        statusProp.Kind.Should().Be(TypeKind.Enum);
        statusProp.EnumValues.Should().BeEquivalentTo("Active", "Inactive", "Pending");
        statusProp.Children.Should().BeNull();
    }

    [Fact(DisplayName = "Maps List<T> and T[] as Collection with element type")]
    public void Maps_Collections()
    {
        var result = typeof(OrderWithList).ToContract();

        var tagsProp = result.Properties!.First(p => p.Name == "Tags");
        tagsProp.Kind.Should().Be(TypeKind.Collection);
        tagsProp.TypeName.Should().Be("List<String>");
        tagsProp.GenericArguments.Should().BeEquivalentTo("String");
        tagsProp.Children.Should().HaveCount(1);
        tagsProp.Children![0].Kind.Should().Be(TypeKind.Primitive);

        var quantitiesProp = result.Properties!.First(p => p.Name == "Quantities");
        quantitiesProp.Kind.Should().Be(TypeKind.Collection);
        quantitiesProp.TypeName.Should().Be("Int32[]");
    }

    [Fact(DisplayName = "Maps Dictionary<K,V> with key and value types")]
    public void Maps_Dictionaries()
    {
        var result = typeof(OrderWithDict).ToContract();

        var metaProp = result.Properties!.First(p => p.Name == "Metadata");
        metaProp.Kind.Should().Be(TypeKind.Dictionary);
        metaProp.TypeName.Should().Be("Dictionary<String, Int32>");
        metaProp.GenericArguments.Should().BeEquivalentTo("String", "Int32");
        metaProp.Children.Should().HaveCount(2);
        metaProp.Children![0].Kind.Should().Be(TypeKind.Primitive);
        metaProp.Children![1].Kind.Should().Be(TypeKind.Primitive);
    }

    [Fact(DisplayName = "Recursively maps nested complex types")]
    public void Maps_Nested_Complex_Types()
    {
        var result = typeof(Customer).ToContract();

        var addressProp = result.Properties!.First(p => p.Name == "BillingAddress");
        addressProp.Kind.Should().Be(TypeKind.Complex);
        addressProp.TypeName.Should().Be("Address");
        addressProp.Children.Should().HaveCount(3);
        addressProp.Children!.Select(c => c.Name).Should().BeEquivalentTo("Street", "City", "Zip");
    }

    [Fact(DisplayName = "Detects cycles and stops recursion")]
    public void Detects_Cycles()
    {
        var result = typeof(CyclicA).ToContract();

        var relatedProp = result.Properties!.First(p => p.Name == "Related");
        relatedProp.Kind.Should().Be(TypeKind.Complex);

        var backProp = relatedProp.Children!.First(p => p.Name == "Back");
        backProp.Kind.Should().Be(TypeKind.Complex);
        backProp.Children.Should().BeNull();
    }

    [Fact(DisplayName = "Stops at max depth and leaves Children null")]
    public void Stops_At_Max_Depth()
    {
        var result = typeof(DeeplyNested).ToContract(maxDepth: 2);

        var level2 = result.Properties!.First(p => p.Name == "Child");
        level2.Kind.Should().Be(TypeKind.Complex);
        level2.Children.Should().NotBeNull();

        var level3 = level2.Children!.First(p => p.Name == "Child");
        level3.Kind.Should().Be(TypeKind.Complex);
        level3.Children.Should().BeNull();
    }

    [Fact(DisplayName = "Generates human-readable generic type names")]
    public void Generates_Readable_Type_Names()
    {
        TypeMapper.GetReadableTypeName(typeof(List<string>)).Should().Be("List<String>");
        TypeMapper.GetReadableTypeName(typeof(Dictionary<string, int>)).Should().Be("Dictionary<String, Int32>");
        TypeMapper.GetReadableTypeName(typeof(int?)).Should().Be("Int32?");
        TypeMapper.GetReadableTypeName(typeof(string)).Should().Be("String");
    }
}
