using FluentAssertions;
using Iris.Brokers.Models;
using Xunit;

namespace Iris.Brokers.Test
{
    public class MessageRequestTests
    {
        [Fact(DisplayName = "MessageRequest.Create should initialize properties correctly")]
        public void Create_ShouldInitializePropertiesCorrectly()
        {
            // Arrange
            string expectedMessageType = "TestType";
            string expectedJson = "{ \"key\": \"value\" }";
            string? expectedFullyQualifiedName = "Iris.Message.Test";
            string? expectedFramework = "MassTransit";
            var expectedHeaders = new Dictionary<string, string> { { "custom-header", "header-value" } };
            var expectedProperties = new Dictionary<string, string> { { "property-key", "property-value" } };

            // Act
            var result = MessageRequest.Create(
                messageType: expectedMessageType,
                json: expectedJson,
                generateIrisHeaders: false,
                messageFullyQualifiedName: expectedFullyQualifiedName,
                framework: expectedFramework,
                headers: expectedHeaders,
                properties: expectedProperties
            );

            // Assert
            result.Should().NotBeNull();
            result.MessageType.Should().Be(expectedMessageType);
            result.Json.Should().Be(expectedJson);
            result.MessageFullyQualifiedName.Should().Be(expectedFullyQualifiedName);
            result.Framework.Should().Be(expectedFramework);
            result.Headers.Should().ContainKey("custom-header").And.ContainValue("header-value");
            result.Properties.Should().ContainKey("property-key").And.ContainValue("property-value");
        }

        [Fact(DisplayName = "MessageRequest.Create should generate Iris headers when requested")]
        public void Create_ShouldGenerateIrisHeaders()
        {
            // Arrange
            string expectedMessageType = "TestType";
            string expectedJson = "{ \"key\": \"value\" }";

            // Act
            var result = MessageRequest.Create(
                messageType: expectedMessageType,
                json: expectedJson,
                generateIrisHeaders: true
            );

            // Assert
            result.Should().NotBeNull();
            result.Headers.Should().ContainKey("iris-key");
            result.Headers["iris-key"].Should().NotBeNullOrEmpty().And.MatchRegex(@"^[0-9a-fA-F-]{36}$"); // UUID format
        }

        [Fact(DisplayName = "MessageRequest.Create should set default dictionaries when null is provided")]
        public void Create_ShouldSetDefaultDictionariesWhenNull()
        {
            // Arrange
            string expectedMessageType = "TestType";
            string expectedJson = "{ \"key\": \"value\" }";

            // Act
            var result = MessageRequest.Create(
                messageType: expectedMessageType,
                json: expectedJson,
                generateIrisHeaders: false
            );

            // Assert
            result.Should().NotBeNull();
            result.Headers.Should().BeEmpty();
            result.Properties.Should().BeEmpty();
        }
    }
}
