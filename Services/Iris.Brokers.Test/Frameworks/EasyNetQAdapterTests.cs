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

    [Fact(DisplayName = "CreateWrappedMessage returns the POCO body unchanged")]
    public void CreateWrappedMessage_ReturnsBodyUnchanged()
    {
        var adapter = new EasyNetQAdapter();
        var request = NewRequest();

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

        request.Headers["type"].Should().Be("MyApp.Messages.OrderPlaced:MyApp.Messages");
    }

    [Fact(DisplayName = "Bare fully-qualified name with no assembly falls back to ':Messages'")]
    public void CreateWrappedMessage_BareFullName_EmitsMessagesFallback()
    {
        var adapter = new EasyNetQAdapter();
        var request = NewRequest(fullyQualifiedName: "MyApp.Messages.OrderPlaced");

        adapter.CreateWrappedMessage(request);

        request.Headers["type"].Should().Be("MyApp.Messages.OrderPlaced:Messages");
    }

    [Fact(DisplayName = "Assembly-qualified name is parsed into EasyNetQ 'FullName:Asm' form")]
    public void CreateWrappedMessage_AssemblyQualifiedName_IsParsed()
    {
        var adapter = new EasyNetQAdapter();
        var request = NewRequest(
            fullyQualifiedName: "MyApp.Messages.OrderPlaced, MyApp.Messages, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");

        adapter.CreateWrappedMessage(request);

        request.Headers["type"].Should().Be("MyApp.Messages.OrderPlaced:MyApp.Messages");
    }

    [Fact(DisplayName = "Sets content_type and persistent delivery_mode")]
    public void CreateWrappedMessage_SetsContentTypeAndDeliveryMode()
    {
        var adapter = new EasyNetQAdapter();
        var request = NewRequest();

        adapter.CreateWrappedMessage(request);

        request.Headers["content_type"].Should().Be("application/json");
        request.Headers["delivery_mode"].Should().Be("2");
    }

    [Fact(DisplayName = "Generates a GUID message_id and mirrors it as correlation_id when none supplied")]
    public void CreateWrappedMessage_GeneratesMessageIdAndCorrelationId()
    {
        var adapter = new EasyNetQAdapter();
        var request = NewRequest();

        adapter.CreateWrappedMessage(request);

        request.Headers.Should().ContainKey("message_id");
        request.Headers["message_id"].Should().MatchRegex(@"^[0-9a-fA-F-]{36}$");
        request.Headers["correlation_id"].Should().Be(request.Headers["message_id"]);
    }

    [Fact(DisplayName = "Preserves a caller-supplied correlation_id")]
    public void CreateWrappedMessage_PreservesCallerCorrelationId()
    {
        var adapter = new EasyNetQAdapter();
        var request = NewRequest(headers: new Dictionary<string, string>
        {
            ["correlation_id"] = "caller-correlation-123",
        });

        adapter.CreateWrappedMessage(request);

        request.Headers["correlation_id"].Should().Be("caller-correlation-123");
        request.Headers["message_id"].Should().NotBe("caller-correlation-123");
    }

    [Fact(DisplayName = "Falls back to MessageType when MessageFullyQualifiedName is null")]
    public void CreateWrappedMessage_FallsBackToMessageType()
    {
        var adapter = new EasyNetQAdapter();
        var request = NewRequest(fullyQualifiedName: null);

        adapter.CreateWrappedMessage(request);

        request.Headers["type"].Should().Be("OrderPlaced:Messages");
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
