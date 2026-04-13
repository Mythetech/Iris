using FluentAssertions;
using Iris.Components.Templates;
using Iris.Contracts.Assemblies.Models;

namespace Iris.Components.Test.Templates;

public class TemplateFieldModelTests
{
    // ── FromPropertyData ──────────────────────────────────────────────────────

    [Fact(DisplayName = "FromPropertyData maps a primitive property")]
    public void FromPropertyData_Primitive_MapsCorrectly()
    {
        var prop = new PropertyData
        {
            Name = "Age",
            TypeName = "int",
            Kind = TypeKind.Primitive,
        };

        var model = TemplateFieldModel.FromPropertyData(prop);

        model.Name.Should().Be("Age");
        model.TypeName.Should().Be("int");
        model.Kind.Should().Be(TypeKind.Primitive);
        model.Value.Should().BeNull();
        model.Expression.Should().BeNull();
        model.Children.Should().BeNull();
        model.Items.Should().BeNull();
        model.PrimitiveItems.Should().BeNull();
        model.HasExpression.Should().BeFalse();
    }

    [Fact(DisplayName = "FromPropertyData maps an enum property with first-value default")]
    public void FromPropertyData_Enum_DefaultsToFirstEnumValue()
    {
        var prop = new PropertyData
        {
            Name = "Status",
            TypeName = "StatusEnum",
            Kind = TypeKind.Enum,
            EnumValues = ["Active", "Inactive", "Pending"],
        };

        var model = TemplateFieldModel.FromPropertyData(prop);

        model.Name.Should().Be("Status");
        model.Kind.Should().Be(TypeKind.Enum);
        model.EnumValues.Should().BeEquivalentTo(["Active", "Inactive", "Pending"]);
        model.Value.Should().Be("Active");
    }

    [Fact(DisplayName = "FromPropertyData maps a complex type with children recursively")]
    public void FromPropertyData_Complex_MapsChildrenRecursively()
    {
        var prop = new PropertyData
        {
            Name = "Address",
            TypeName = "Address",
            Kind = TypeKind.Complex,
            Children =
            [
                new PropertyData { Name = "Street", TypeName = "string", Kind = TypeKind.Primitive },
                new PropertyData { Name = "City", TypeName = "string", Kind = TypeKind.Primitive },
            ]
        };

        var model = TemplateFieldModel.FromPropertyData(prop);

        model.Name.Should().Be("Address");
        model.Kind.Should().Be(TypeKind.Complex);
        model.Children.Should().HaveCount(2);
        model.Children![0].Name.Should().Be("Street");
        model.Children[0].Kind.Should().Be(TypeKind.Primitive);
        model.Children[1].Name.Should().Be("City");
    }

    [Fact(DisplayName = "FromPropertyData maps a collection property")]
    public void FromPropertyData_Collection_MapsCorrectly()
    {
        var prop = new PropertyData
        {
            Name = "Tags",
            TypeName = "List<string>",
            Kind = TypeKind.Collection,
            GenericArguments = ["string"],
        };

        var model = TemplateFieldModel.FromPropertyData(prop);

        model.Name.Should().Be("Tags");
        model.Kind.Should().Be(TypeKind.Collection);
        model.TypeName.Should().Be("List<string>");
        model.GenericArguments.Should().BeEquivalentTo(["string"]);
    }

    [Fact(DisplayName = "FromPropertyData maps a complex collection with no children")]
    public void FromPropertyData_ComplexCollection_HasNullChildren()
    {
        var prop = new PropertyData
        {
            Name = "Items",
            TypeName = "List<Order>",
            Kind = TypeKind.Collection,
            GenericArguments = ["Order"],
        };

        var model = TemplateFieldModel.FromPropertyData(prop);

        model.Kind.Should().Be(TypeKind.Collection);
        model.Children.Should().BeNull();
        model.Items.Should().BeNull();
    }

    // ── ToJson ────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "ToJson serializes a primitive static value")]
    public void ToJson_PrimitiveStaticValue_SerializesCorrectly()
    {
        var fields = new List<TemplateFieldModel>
        {
            new() { Name = "Age", TypeName = "int", Kind = TypeKind.Primitive, Value = 42 }
        };

        var json = TemplateFieldModel.ToJson(fields);

        var doc = System.Text.Json.JsonDocument.Parse(json);
        doc.RootElement.GetProperty("Age").GetInt32().Should().Be(42);
    }

    [Fact(DisplayName = "ToJson serializes a string static value")]
    public void ToJson_StringStaticValue_SerializesCorrectly()
    {
        var fields = new List<TemplateFieldModel>
        {
            new() { Name = "Name", TypeName = "string", Kind = TypeKind.Primitive, Value = "Alice" }
        };

        var json = TemplateFieldModel.ToJson(fields);

        var doc = System.Text.Json.JsonDocument.Parse(json);
        doc.RootElement.GetProperty("Name").GetString().Should().Be("Alice");
    }

    [Fact(DisplayName = "ToJson serializes an expression as {{Expression}}")]
    public void ToJson_Expression_SerializesAsBraces()
    {
        var fields = new List<TemplateFieldModel>
        {
            new() { Name = "Id", TypeName = "Guid", Kind = TypeKind.Primitive, Expression = "NewGuid()" }
        };

        var json = TemplateFieldModel.ToJson(fields);

        var doc = System.Text.Json.JsonDocument.Parse(json);
        doc.RootElement.GetProperty("Id").GetString().Should().Be("{{NewGuid()}}");
    }

    [Fact(DisplayName = "ToJson serializes nested complex object")]
    public void ToJson_NestedComplexObject_SerializesCorrectly()
    {
        var fields = new List<TemplateFieldModel>
        {
            new()
            {
                Name = "Address",
                TypeName = "Address",
                Kind = TypeKind.Complex,
                Children =
                [
                    new() { Name = "Street", TypeName = "string", Kind = TypeKind.Primitive, Value = "123 Main St" },
                    new() { Name = "City", TypeName = "string", Kind = TypeKind.Primitive, Value = "Springfield" },
                ]
            }
        };

        var json = TemplateFieldModel.ToJson(fields);

        var doc = System.Text.Json.JsonDocument.Parse(json);
        var address = doc.RootElement.GetProperty("Address");
        address.GetProperty("Street").GetString().Should().Be("123 Main St");
        address.GetProperty("City").GetString().Should().Be("Springfield");
    }

    [Fact(DisplayName = "ToJson serializes collection of complex items")]
    public void ToJson_ComplexCollection_SerializesAsArray()
    {
        var fields = new List<TemplateFieldModel>
        {
            new()
            {
                Name = "Orders",
                TypeName = "List<Order>",
                Kind = TypeKind.Collection,
                GenericArguments = ["Order"],
                Items =
                [
                    [
                        new() { Name = "Id", TypeName = "int", Kind = TypeKind.Primitive, Value = 1 },
                        new() { Name = "Total", TypeName = "decimal", Kind = TypeKind.Primitive, Value = 9.99m }
                    ]
                ]
            }
        };

        var json = TemplateFieldModel.ToJson(fields);

        var doc = System.Text.Json.JsonDocument.Parse(json);
        var orders = doc.RootElement.GetProperty("Orders");
        orders.GetArrayLength().Should().Be(1);
        orders[0].GetProperty("Id").GetInt32().Should().Be(1);
    }

    [Fact(DisplayName = "ToJson serializes primitive collection as string array")]
    public void ToJson_PrimitiveCollection_SerializesAsStringArray()
    {
        var fields = new List<TemplateFieldModel>
        {
            new()
            {
                Name = "Tags",
                TypeName = "List<string>",
                Kind = TypeKind.Collection,
                GenericArguments = ["string"],
                PrimitiveItems = ["foo", "bar", "baz"]
            }
        };

        var json = TemplateFieldModel.ToJson(fields);

        var doc = System.Text.Json.JsonDocument.Parse(json);
        var tags = doc.RootElement.GetProperty("Tags");
        tags.GetArrayLength().Should().Be(3);
        tags[0].GetString().Should().Be("foo");
        tags[1].GetString().Should().Be("bar");
        tags[2].GetString().Should().Be("baz");
    }

    [Fact(DisplayName = "ToJson serializes enum value")]
    public void ToJson_EnumValue_SerializesAsString()
    {
        var fields = new List<TemplateFieldModel>
        {
            new()
            {
                Name = "Status",
                TypeName = "StatusEnum",
                Kind = TypeKind.Enum,
                Value = "Active",
                EnumValues = ["Active", "Inactive"]
            }
        };

        var json = TemplateFieldModel.ToJson(fields);

        var doc = System.Text.Json.JsonDocument.Parse(json);
        doc.RootElement.GetProperty("Status").GetString().Should().Be("Active");
    }

    [Fact(DisplayName = "ToJson emits null for field with no value or expression")]
    public void ToJson_NoValueOrExpression_EmitsNull()
    {
        var fields = new List<TemplateFieldModel>
        {
            new() { Name = "Notes", TypeName = "string", Kind = TypeKind.Primitive }
        };

        var json = TemplateFieldModel.ToJson(fields);

        var doc = System.Text.Json.JsonDocument.Parse(json);
        doc.RootElement.GetProperty("Notes").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
    }

    // ── FromJson ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "FromJson maps static string value")]
    public void FromJson_StaticStringValue_MapsCorrectly()
    {
        var json = """{"Name": "Alice"}""";

        var fields = TemplateFieldModel.FromJson(json);

        fields.Should().HaveCount(1);
        var f = fields[0];
        f.Name.Should().Be("Name");
        f.Value.Should().Be("Alice");
        f.Expression.Should().BeNull();
        f.HasExpression.Should().BeFalse();
    }

    [Fact(DisplayName = "FromJson detects {{...}} expression and sets Expression property")]
    public void FromJson_ExpressionValue_SetsExpression()
    {
        var json = """{"Id": "{{NewGuid()}}"}""";

        var fields = TemplateFieldModel.FromJson(json);

        fields.Should().HaveCount(1);
        var f = fields[0];
        f.Expression.Should().Be("NewGuid()");
        f.Value.Should().BeNull();
        f.HasExpression.Should().BeTrue();
    }

    [Fact(DisplayName = "FromJson maps nested object to child fields")]
    public void FromJson_NestedObject_MapsToChildren()
    {
        var json = """{"Address": {"Street": "123 Main St", "City": "Springfield"}}""";

        var fields = TemplateFieldModel.FromJson(json);

        fields.Should().HaveCount(1);
        var address = fields[0];
        address.Name.Should().Be("Address");
        address.Kind.Should().Be(TypeKind.Complex);
        address.Children.Should().HaveCount(2);
        address.Children![0].Name.Should().Be("Street");
        address.Children[0].Value.Should().Be("123 Main St");
        address.Children[1].Name.Should().Be("City");
    }

    [Fact(DisplayName = "FromJson maps array of objects to Items")]
    public void FromJson_ArrayOfObjects_MapsToItems()
    {
        var json = """{"Orders": [{"Id": 1, "Total": 9.99}]}""";

        var fields = TemplateFieldModel.FromJson(json);

        fields.Should().HaveCount(1);
        var orders = fields[0];
        orders.Name.Should().Be("Orders");
        orders.Kind.Should().Be(TypeKind.Collection);
        orders.Items.Should().HaveCount(1);
        orders.Items![0].Should().HaveCount(2);
        orders.Items[0][0].Name.Should().Be("Id");
    }

    [Fact(DisplayName = "FromJson maps array of strings to PrimitiveItems")]
    public void FromJson_ArrayOfStrings_MapsToPrimitiveItems()
    {
        var json = """{"Tags": ["foo", "bar", "baz"]}""";

        var fields = TemplateFieldModel.FromJson(json);

        fields.Should().HaveCount(1);
        var tags = fields[0];
        tags.Name.Should().Be("Tags");
        tags.Kind.Should().Be(TypeKind.Collection);
        tags.PrimitiveItems.Should().BeEquivalentTo(["foo", "bar", "baz"]);
        tags.Items.Should().BeNull();
    }

    [Fact(DisplayName = "FromJson maps integer value correctly")]
    public void FromJson_IntegerValue_MapsCorrectly()
    {
        var json = """{"Age": 42}""";

        var fields = TemplateFieldModel.FromJson(json);

        fields.Should().HaveCount(1);
        var f = fields[0];
        f.Name.Should().Be("Age");
        f.Value.Should().NotBeNull();
        f.Expression.Should().BeNull();
    }

    [Fact(DisplayName = "FromJson maps boolean value correctly")]
    public void FromJson_BooleanValue_MapsCorrectly()
    {
        var json = """{"Active": true}""";

        var fields = TemplateFieldModel.FromJson(json);

        fields.Should().HaveCount(1);
        fields[0].Value.Should().NotBeNull();
        fields[0].Expression.Should().BeNull();
    }

    [Fact(DisplayName = "HasExpression is false when only Value is set")]
    public void HasExpression_WhenValueSet_IsFalse()
    {
        var model = new TemplateFieldModel { Name = "X", TypeName = "string", Value = "hello" };

        model.HasExpression.Should().BeFalse();
    }

    [Fact(DisplayName = "HasExpression is true when Expression is non-null")]
    public void HasExpression_WhenExpressionSet_IsTrue()
    {
        var model = new TemplateFieldModel { Name = "X", TypeName = "string", Expression = "NewGuid()" };

        model.HasExpression.Should().BeTrue();
    }

    [Fact(DisplayName = "HasExpression is false when Expression is null")]
    public void HasExpression_WhenExpressionNull_IsFalse()
    {
        var model = new TemplateFieldModel { Name = "X", TypeName = "string", Expression = null };

        model.HasExpression.Should().BeFalse();
    }

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Round-trip: ToJson then FromJson preserves static values")]
    public void RoundTrip_StaticValues_Preserved()
    {
        var original = new List<TemplateFieldModel>
        {
            new() { Name = "Name", TypeName = "string", Kind = TypeKind.Primitive, Value = "Alice" },
            new() { Name = "Age", TypeName = "int", Kind = TypeKind.Primitive, Value = 30 }
        };

        var json = TemplateFieldModel.ToJson(original);
        var restored = TemplateFieldModel.FromJson(json);

        restored.Should().HaveCount(2);
        restored[0].Name.Should().Be("Name");
        restored[0].Value.Should().Be("Alice");
        restored[1].Name.Should().Be("Age");
    }

    [Fact(DisplayName = "Round-trip: ToJson then FromJson preserves expressions")]
    public void RoundTrip_Expressions_Preserved()
    {
        var original = new List<TemplateFieldModel>
        {
            new() { Name = "Id", TypeName = "Guid", Kind = TypeKind.Primitive, Expression = "NewGuid()" }
        };

        var json = TemplateFieldModel.ToJson(original);
        var restored = TemplateFieldModel.FromJson(json);

        restored.Should().HaveCount(1);
        restored[0].Expression.Should().Be("NewGuid()");
        restored[0].HasExpression.Should().BeTrue();
    }
}
