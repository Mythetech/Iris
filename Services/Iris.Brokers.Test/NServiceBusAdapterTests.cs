using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Iris.Brokers.Frameworks;
using Iris.Brokers.Models;
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
    }
}
