using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Iris.Brokers.Frameworks;
using Iris.Brokers.Models;
using Iris.Brokers.Test.Frameworks;
using NServiceBus.Transport;
using Xunit;

namespace Iris.Brokers.Test
{
    public class NServiceBusAdapterTests
    {
        [Fact(DisplayName = "NServiceBus.Version header should reflect the loaded NServiceBus.Transport assembly version, not a hardcoded value")]
        public void CreateWrappedMessage_ShouldEmitVersionHeaderFromLoadedAssembly()
        {
            // Arrange
            var adapter = new NServiceBusAdapter();
            var request = MessageRequest.Create(
                messageType: "MyApp.Commands.DoThing",
                json: "{\"name\":\"value\"}",
                generateIrisHeaders: false,
                messageFullyQualifiedName: "MyApp.Commands.DoThing");

            var expectedVersion = typeof(OutgoingMessage).Assembly.GetName().Version!.ToString(3);

            // Act
            var wrapped = adapter.CreateWrappedMessage(request);

            // Assert
            using var doc = JsonDocument.Parse(wrapped);
            var headers = doc.RootElement.GetProperty("Headers");
            headers.GetProperty("NServiceBus.Version").GetString()
                .Should().Be(expectedVersion);
        }

        [Fact(DisplayName = "EnclosedMessageTypes is assembly qualified when an assembly name is supplied")]
        public void CreateWrappedMessage_WithAssembly_QualifiesEnclosedMessageTypes()
        {
            var request = MessageRequest.Create(
                messageType: "DoThing",
                json: "{}",
                generateIrisHeaders: false,
                messageFullyQualifiedName: "MyApp.Commands.DoThing",
                messageAssemblyName: "MyApp.Commands");

            using var doc = JsonDocument.Parse(new NServiceBusAdapter().CreateWrappedMessage(request));

            doc.RootElement.GetProperty("Headers").GetProperty("NServiceBus.EnclosedMessageTypes").GetString()
                .Should().Be("MyApp.Commands.DoThing, MyApp.Commands");
        }

        [Fact(DisplayName = "Declares only body keys")]
        public void Keys_AreBodyOnly()
        {
            var adapter = new NServiceBusAdapter();
            adapter.Keys.Should().NotBeEmpty();
            adapter.Keys.Should().OnlyContain(k => k.Location == KeyLocation.Body);
            FrameworkKeyAssertions.AssertKeysMatchWrite(adapter, MessageRequest.Create("T", "{}", false, "N.T"));
        }
    }
}
