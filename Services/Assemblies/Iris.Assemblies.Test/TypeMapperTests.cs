using FluentAssertions;
using Iris.Assemblies;
using Xunit;

namespace Iris.Api.Test.Assemblies;

public record SimpleTestType(string Name, int Amount, object Value);

public record ComplexType(List<object> Stuff, dynamic Things);

public class TypeMapperTests
{
    [Theory(DisplayName = "Can map given Type and properties to contract model")]
    [InlineData(typeof(SimpleTestType), "Name", "Amount", "Value")]
    [InlineData(typeof(ComplexType), "Stuff", "Things")]
    public void Can_MapType_ToContractModel(Type type, params string[] props)
    {
        var contractModel = type.ToContract();
        
        contractModel.Name.Should().Be(type.Name);
        contractModel.FullyQualifiedName.Should().Be(type.FullName);
        contractModel.Properties.Count.Should().Be(props.Length);
        foreach (var prop in props)
        {
            contractModel.Properties.Should().ContainKey(prop);
        }
    }
}