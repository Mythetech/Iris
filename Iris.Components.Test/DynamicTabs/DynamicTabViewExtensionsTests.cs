using FluentAssertions;
using Iris.Components.Shared.DynamicTabs;

namespace Iris.Components.Test.DynamicTabs;

public class DynamicTabViewExtensionsTests
{
    [Fact(DisplayName = "ToSerializedModel should serialize correctly and remove dynamic instances")]
    public void ToSerializedModel_ShouldSerializeCorrectly()
    {
        // Arrange
        var dynamicTab = new BasicDynamicTabView()
        {
            Name = "Test Tab",
            AreaIdentifier = "MainPanel",
            AreaIndex = 0,
            BadgeCount = 3,
            IsActive = true,
            ComponentType = typeof(DynamicTabView),
            Parameters = new Dictionary<string, object>
            {
                { "Key1", "Value1" },
                { "Instance", new BasicDynamicTabView() }
            }
        };

        // Act
        var serializedModel = dynamicTab.ToSerializedModel();

        // Assert
        serializedModel.Id.Should().Be(dynamicTab.Id);
        serializedModel.Name.Should().Be(dynamicTab.Name);
        serializedModel.AreaIdentifier.Should().Be(dynamicTab.AreaIdentifier);
        serializedModel.AreaIndex.Should().Be(dynamicTab.AreaIndex);
        serializedModel.BadgeCount.Should().Be(dynamicTab.BadgeCount);
        serializedModel.IsActive.Should().Be(dynamicTab.IsActive);
        serializedModel.ComponentType.Should().Be(dynamicTab.ComponentType.AssemblyQualifiedName);
        serializedModel.Parameters.Should().NotContainKey("Instance");
    }

       [Fact(DisplayName = "ToModel should deserialize correctly")]
        public void ToModel_ShouldDeserializeCorrectly()
        {
            // Arrange
            var serializedModel = new SerializedDynamicTabModel
            {
                Name = "Test Tab",
                AreaIdentifier = "MainPanel",
                AreaIndex = 0,
                BadgeCount = 3,
                IsActive = true,
                ComponentType = typeof(BasicDynamicTabView).AssemblyQualifiedName,
                Parameters = new Dictionary<string, object>
                {
                    { "Key1", "Value1" }
                }
            };

            // Act
            var deserializedTab = serializedModel.ToModel();

            // Assert
            deserializedTab.Id.Should().Be(serializedModel.Id);
            deserializedTab.Name.Should().Be(serializedModel.Name);
            deserializedTab.AreaIdentifier.Should().Be(serializedModel.AreaIdentifier);
            deserializedTab.AreaIndex.Should().Be(serializedModel.AreaIndex);
            deserializedTab.BadgeCount.Should().Be(serializedModel.BadgeCount);
            deserializedTab.IsActive.Should().Be(serializedModel.IsActive);
            deserializedTab.Parameters.Should().BeEquivalentTo(serializedModel.Parameters);
            deserializedTab.Parameters.Should().ContainKey("Instance"); // Ensure "Instance" is added
        }

        [Fact(DisplayName = "ToModel should throw exception if type is not found")]
        public void ToModel_ShouldThrowExceptionIfTypeNotFound()
        {
            // Arrange
            var serializedModel = new SerializedDynamicTabModel
            {
                ComponentType = "NonExistentType"
            };

            // Act
            Action action = () => serializedModel.ToModel();

            // Assert
            action.Should().Throw<InvalidOperationException>()
                .WithMessage("Type NonExistentType could not be found.");
        }

        [Fact(DisplayName = "ToModel should throw exception if type is not DynamicTabView")]
        public void ToModel_ShouldThrowExceptionIfTypeIsNotDynamicTabView()
        {
            // Arrange
            var serializedModel = new SerializedDynamicTabModel
            {
                ComponentType = typeof(object).AssemblyQualifiedName
            };

            // Act
            Action action = () => serializedModel.ToModel();

            // Assert
            action.Should().Throw<InvalidOperationException>()
                .WithMessage("Type System.Object*is not a DynamicTabView.");
        }
    }
