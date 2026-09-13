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

        [Fact(DisplayName = "MessageRequest.Create copies the header and property dictionaries")]
        public void Create_CopiesDictionaries()
        {
            var headers = new Dictionary<string, string> { ["a"] = "1" };
            var properties = new Dictionary<string, string> { ["p"] = "x" };

            var result = MessageRequest.Create("T", "{}", generateIrisHeaders: false, headers: headers, properties: properties);
            result.Headers["b"] = "2";
            result.Properties["q"] = "y";

            headers.Should().NotContainKey("b");
            properties.Should().NotContainKey("q");
            result.Headers.Should().ContainKey("a");
        }

        [Fact(DisplayName = "Two requests created from the same headers get distinct iris keys")]
        public void Create_TwiceWithSameHeaders_GivesDistinctIrisKeys()
        {
            var headers = new Dictionary<string, string>();

            var first = MessageRequest.Create("T", "{}", generateIrisHeaders: true, headers: headers);
            var second = MessageRequest.Create("T", "{}", generateIrisHeaders: true, headers: headers);

            first.Headers["iris-key"].Should().NotBe(second.Headers["iris-key"]);
            headers.Should().BeEmpty();
        }

        [Fact(DisplayName = "TransportProperties start empty and report what is set")]
        public void TransportProperties_TrackSetMembers()
        {
            var result = MessageRequest.Create("T", "{}", generateIrisHeaders: false);

            result.TransportProperties.SetProperties().Should().BeEmpty();

            result.TransportProperties.Type = "MyType";
            result.TransportProperties.Persistent = true;

            result.TransportProperties.SetProperties().Should().BeEquivalentTo(
                new[] { TransportProperty.Type, TransportProperty.Persistent });
            result.TransportProperties.IsSet(TransportProperty.MessageId).Should().BeFalse();

            result.TransportProperties.Clear(TransportProperty.Type);
            result.TransportProperties.IsSet(TransportProperty.Type).Should().BeFalse();
        }

        [Fact(DisplayName = "HeaderTypeOf defaults to String for undeclared keys")]
        public void HeaderTypeOf_DefaultsToString()
        {
            var result = MessageRequest.Create("T", "{}", generateIrisHeaders: false);

            result.HeaderTypeOf("anything").Should().Be(HeaderDataType.String);
        }
    }
}
