using FluentAssertions;
using Iris.Assemblies;
using Iris.Contracts.Assemblies.Models;
using Xunit;

namespace Iris.Api.Test.Assemblies;

public class AssemblyMapperTests
{
    [Fact(DisplayName = "Can map assembly to contract model")]
    public void Can_Map_AssemblyToContract()
    {
        // Arrange
        var assembly = typeof(AssemblyMapperTests).Assembly;

        // Act
        var contract = assembly.ToContract();

        // Assert
        contract.Name.Should().Be("Iris.Assemblies.Test");
        contract.ExportedTypes.Should().HaveCount(assembly.ExportedTypes.Count());
    }

    [Fact(DisplayName = "Maps exported types with their properties")]
    public void Maps_ExportedTypes_With_Properties()
    {
        // Arrange
        var assembly = typeof(AssemblyMapperTests).Assembly;

        // Act
        var contract = assembly.ToContract();

        // Assert
        contract.ExportedTypes.Should().NotBeEmpty();

        var typeData = contract.ExportedTypes.First();
        typeData.Name.Should().NotBeNullOrEmpty();
        typeData.FullyQualifiedName.Should().NotBeNullOrEmpty();
        // Properties should be a List<PropertyData>, not a Dictionary
        typeData.Properties.Should().BeOfType<List<PropertyData>>();
    }

    [Fact(DisplayName = "Correctly maps PropertyData for a type")]
    public void Maps_PropertyData_Correctly()
    {
        // Arrange
        var testType = typeof(SimpleTestRecord);

        // Act
        var contract = testType.ToContract();

        // Assert
        contract.Properties.Should().NotBeNull();
        contract.Properties.Should().HaveCount(3);

        // Verify PropertyData uses Name field, not dictionary keys
        contract.Properties!.Should().Contain(p => p.Name == "Name");
        contract.Properties!.Should().Contain(p => p.Name == "Age");
        contract.Properties!.Should().Contain(p => p.Name == "Active");
    }
}

// Test record for property mapping verification
public record SimpleTestRecord(string Name, int Age, bool Active);