using FluentAssertions;
using Iris.Assemblies;
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
        contract.ExportedTypes.Count().Should().Be(assembly.ExportedTypes.Count());
    }
}