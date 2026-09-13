using FluentAssertions;
using Iris.Brokers.Extensions;
using Iris.Brokers.Frameworks;
using Iris.Brokers.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Iris.Brokers.Test.Frameworks;

public class EasyNetQAdapterTests
{
    private const string SampleJson = "{\"orderId\":42,\"amount\":9.99}";

    private static MessageRequest NewRequest(
        string? fullyQualifiedName = "MyApp.Messages.OrderPlaced",
        string? assemblyName = null,
        Dictionary<string, string>? headers = null) =>
        new()
        {
            MessageType = "OrderPlaced",
            MessageFullyQualifiedName = fullyQualifiedName,
            MessageAssemblyName = assemblyName,
            Json = SampleJson,
            Framework = "EasyNetQ",
            Headers = headers ?? new Dictionary<string, string>(),
        };

    [Fact(DisplayName = "Declared keys match what CreateWrappedMessage writes")]
    public void Keys_MatchWrite()
    {
        FrameworkKeyAssertions.AssertKeysMatchWrite(new EasyNetQAdapter(), NewRequest(assemblyName: "MyApp.Messages"));
    }

    [Fact(DisplayName = "Type and content type are transport properties, not headers")]
    public void CreateWrappedMessage_WritesTransportProperties_NotHeaders()
    {
        var request = NewRequest(assemblyName: "MyApp.Messages");

        new EasyNetQAdapter().CreateWrappedMessage(request);

        request.TransportProperties.Type.Should().Be("MyApp.Messages.OrderPlaced, MyApp.Messages");
        request.TransportProperties.ContentType.Should().Be("application/json");
        request.TransportProperties.MessageId.Should().MatchRegex(@"^[0-9a-fA-F-]{36}$");
        request.TransportProperties.CorrelationId.Should().Be(request.TransportProperties.MessageId);
        request.TransportProperties.Persistent.Should().BeTrue();
        request.TransportProperties.Timestamp.Should().NotBeNull();
        request.Headers.Should().BeEmpty();
    }

    [Fact(DisplayName = "A missing assembly name is an error, never a guessed ':Messages' suffix")]
    public void CreateWrappedMessage_NoAssembly_Throws()
    {
        var request = NewRequest(fullyQualifiedName: "MyApp.Messages.OrderPlaced", assemblyName: null);

        var act = () => new EasyNetQAdapter().CreateWrappedMessage(request);

        act.Should().Throw<ArgumentException>().WithMessage("*assembly*");
    }

    [Fact(DisplayName = "CreateWrappedMessage returns the POCO body unchanged")]
    public void CreateWrappedMessage_ReturnsBodyUnchanged()
    {
        var adapter = new EasyNetQAdapter();
        var request = NewRequest(assemblyName: "MyApp.Messages");

        var result = adapter.CreateWrappedMessage(request);

        result.Should().Be(SampleJson);
    }

    [Fact(DisplayName = "Uses MessageAssemblyName when supplied")]
    public void CreateWrappedMessage_UsesMessageAssemblyName()
    {
        var adapter = new EasyNetQAdapter();
        var request = NewRequest(
            fullyQualifiedName: "MyApp.Messages.OrderPlaced",
            assemblyName: "MyApp.Messages");

        adapter.CreateWrappedMessage(request);

        request.TransportProperties.Type.Should().Be("MyApp.Messages.OrderPlaced, MyApp.Messages");
    }

    [Fact(DisplayName = "Assembly-qualified name is parsed into EasyNetQ 'FullName, Asm' form")]
    public void CreateWrappedMessage_AssemblyQualifiedName_IsParsed()
    {
        var adapter = new EasyNetQAdapter();
        var request = NewRequest(
            fullyQualifiedName: "MyApp.Messages.OrderPlaced, MyApp.Messages, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");

        adapter.CreateWrappedMessage(request);

        request.TransportProperties.Type.Should().Be("MyApp.Messages.OrderPlaced, MyApp.Messages");
    }

    [Fact(DisplayName = "Generates a GUID message id and mirrors it as correlation id when none supplied")]
    public void CreateWrappedMessage_GeneratesMessageIdAndCorrelationId()
    {
        var adapter = new EasyNetQAdapter();
        var request = NewRequest(assemblyName: "MyApp.Messages");

        adapter.CreateWrappedMessage(request);

        request.TransportProperties.MessageId.Should().MatchRegex(@"^[0-9a-fA-F-]{36}$");
        request.TransportProperties.CorrelationId.Should().Be(request.TransportProperties.MessageId);
    }

    [Fact(DisplayName = "Preserves a caller-supplied correlation id")]
    public void CreateWrappedMessage_PreservesCallerCorrelationId()
    {
        var adapter = new EasyNetQAdapter();
        var request = NewRequest(assemblyName: "MyApp.Messages");
        request.TransportProperties.CorrelationId = "caller-correlation-123";

        adapter.CreateWrappedMessage(request);

        request.TransportProperties.CorrelationId.Should().Be("caller-correlation-123");
        request.TransportProperties.MessageId.Should().NotBe("caller-correlation-123");
    }

    [Fact(DisplayName = "Falls back to MessageType when MessageFullyQualifiedName is null")]
    public void CreateWrappedMessage_FallsBackToMessageType()
    {
        var adapter = new EasyNetQAdapter();
        var request = NewRequest(fullyQualifiedName: null, assemblyName: "MyApp.Messages");

        adapter.CreateWrappedMessage(request);

        request.TransportProperties.Type.Should().Be("OrderPlaced, MyApp.Messages");
    }

    [Fact(DisplayName = "Properties[TypeNameFormatKey] = 'Legacy' uses the colon-separated LegacyTypeNameSerializer form")]
    public void CreateWrappedMessage_LegacyFormat_UsesColon()
    {
        var adapter = new EasyNetQAdapter();
        var request = NewRequest(assemblyName: "MyApp.Messages");
        request.Properties[EasyNetQAdapter.TypeNameFormatKey] = "Legacy";

        adapter.CreateWrappedMessage(request);

        request.TransportProperties.Type.Should().Be("MyApp.Messages.OrderPlaced:MyApp.Messages");
    }

    [Fact(DisplayName = "Throws when Json is empty")]
    public void CreateWrappedMessage_ThrowsOnEmptyJson()
    {
        var adapter = new EasyNetQAdapter();
        var request = NewRequest();
        request.Json = "";

        var act = () => adapter.CreateWrappedMessage(request);

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Throws when MessageType is empty")]
    public void CreateWrappedMessage_ThrowsOnEmptyMessageType()
    {
        var adapter = new EasyNetQAdapter();
        var request = NewRequest();
        request.MessageType = "";

        var act = () => adapter.CreateWrappedMessage(request);

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Adapter is discovered by AddFrameworkProvider and resolvable by name")]
    public void Registration_DiscoversEasyNetQAdapter()
    {
        var services = new ServiceCollection();
        services.AddFrameworkProvider();
        using var provider = services.BuildServiceProvider();

        var frameworkProvider = provider.GetRequiredService<IFrameworkProvider>();
        var framework = frameworkProvider.GetFramework("EasyNetQ");

        framework.Should().NotBeNull();
        framework.Should().BeOfType<EasyNetQAdapter>();
        framework!.Name.Should().Be("EasyNetQ");
    }
}
