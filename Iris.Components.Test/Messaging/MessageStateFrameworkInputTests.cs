using FluentAssertions;
using Iris.Components.Messaging;
using Iris.Contracts.Assemblies.Models;
using Iris.Contracts.Messaging.Frameworks;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;

namespace Iris.Components.Test.Messaging;

public class MessageStateFrameworkInputTests
{
    private static readonly FrameworkDescriptor Rebus = new("Rebus",
    [
        new FrameworkInput("TypeName", "Type name", "fqn"),
        new FrameworkInput("AssemblyName", "Assembly name", "asm"),
    ]);

    private static readonly FrameworkDescriptor Brighter = new("Brighter",
    [
        new FrameworkInput("TypeName", "Type name", "fqn"),
        new FrameworkInput("Topic", "Topic", "topic"),
        new FrameworkInput("BrighterMessageType", "Message kind", "kind", DefaultValue: "MT_EVENT", AllowedValues: ["MT_EVENT", "MT_COMMAND"]),
    ]);

    private static MessageState NewState() => new(new MessagingSettings(), Substitute.For<IMessageBus>());

    [Fact(DisplayName = "Starts with no property rows")]
    public void Starts_Empty()
    {
        NewState().AdditionalProperties.Should().BeEmpty();
    }

    [Fact(DisplayName = "Selecting a framework seeds its inputs with defaults and descriptions")]
    public void SetFramework_SeedsRows()
    {
        var state = NewState();

        state.SetFramework(Brighter);

        state.SelectedFramework.Should().Be("Brighter");
        state.AdditionalProperties.Select(r => r.Key).Should().Equal("TypeName", "Topic", "BrighterMessageType");
        var kind = state.AdditionalProperties.Single(r => r.Key == "BrighterMessageType");
        kind.Value.Should().Be("MT_EVENT");
        kind.Description.Should().Be("kind");
        kind.AllowedValues.Should().Equal("MT_EVENT", "MT_COMMAND");
    }

    [Fact(DisplayName = "Switching frameworks keeps typed values for surviving keys and removes the rest")]
    public void SetFramework_Switch_PreservesAndRemoves()
    {
        var state = NewState();
        state.SetFramework(Brighter);
        state.AdditionalProperties.Single(r => r.Key == "TypeName").Value = "MyApp.Order";
        state.AdditionalProperties.Single(r => r.Key == "Topic").Value = "orders";

        state.SetFramework(Rebus);

        state.AdditionalProperties.Select(r => r.Key).Should().BeEquivalentTo(new[] { "TypeName", "AssemblyName" });
        state.AdditionalProperties.Single(r => r.Key == "TypeName").Value.Should().Be("MyApp.Order");
    }

    [Fact(DisplayName = "Rows the user added by hand survive a switch and a clear")]
    public void SetFramework_KeepsCustomRows()
    {
        var state = NewState();
        state.SetFramework(Brighter);
        state.AddAdditionalProperty(new DictionaryViewModel { Key = "mine", Value = "1" });

        state.SetFramework(Rebus);
        state.AdditionalProperties.Should().Contain(r => r.Key == "mine");

        state.SetFramework(null);
        state.SelectedFramework.Should().BeNull();
        state.AdditionalProperties.Select(r => r.Key).Should().Equal("mine");
    }

    [Fact(DisplayName = "Picking a type seeds only the rows the framework declares")]
    public void SeedTypeInputs_FillsDeclaredRows()
    {
        var state = NewState();
        state.SetFramework(Rebus);

        state.SeedTypeInputs(new TypeData { Name = "OrderPlaced", FullyQualifiedName = "MyApp.Messages.OrderPlaced" }, "MyApp.Messages");

        state.AdditionalProperties.Single(r => r.Key == "TypeName").Value.Should().Be("MyApp.Messages.OrderPlaced");
        state.AdditionalProperties.Single(r => r.Key == "AssemblyName").Value.Should().Be("MyApp.Messages");
    }

    [Fact(DisplayName = "Picking a type never adds rows the framework did not declare")]
    public void SeedTypeInputs_DoesNotAddRows()
    {
        var state = NewState();
        state.SetFramework(new FrameworkDescriptor("MassTransit", [new FrameworkInput("TypeName", "Type name", "fqn")]));

        state.SeedTypeInputs(new TypeData { Name = "OrderPlaced", FullyQualifiedName = "MyApp.Messages.OrderPlaced" }, "MyApp.Messages");

        state.AdditionalProperties.Select(r => r.Key).Should().Equal("TypeName");
    }

    [Fact(DisplayName = "Available frameworks that no longer support the selection clear it with a notice")]
    public void SetAvailableFrameworks_ClearsUnsupportedSelection()
    {
        var state = NewState();
        state.SetFramework(Rebus);

        state.SetAvailableFrameworks([Rebus with { Supported = false, UnsupportedReason = "Rebus needs transport headers; Azure cannot carry them." }]);

        state.SelectedFramework.Should().BeNull();
        state.FrameworkNotice.Should().Be("Rebus needs transport headers; Azure cannot carry them.");
        state.AvailableFrameworks.Should().ContainSingle(f => f.Name == "Rebus" && !f.Supported);
    }

    [Fact(DisplayName = "Available frameworks that drop the selection entirely clear it with a notice")]
    public void SetAvailableFrameworks_ClearsSelectionMissingFromCatalog()
    {
        var state = NewState();
        state.SetFramework(Rebus);

        state.SetAvailableFrameworks([new FrameworkDescriptor("MassTransit", [])]);

        state.SelectedFramework.Should().BeNull();
        state.FrameworkNotice.Should().Be("Rebus is not available for this connection.");
    }

    [Fact(DisplayName = "GetHeaders ignores blank keys and keeps the last duplicate")]
    public void GetHeaders_Tolerant()
    {
        var state = new MessageState(new MessagingSettings { SendIrisHeader = false }, Substitute.For<IMessageBus>());
        state.AddHeader(new DictionaryViewModel { Key = "", Value = "x" });
        state.AddHeader(new DictionaryViewModel { Key = "a", Value = "1" });
        state.AddHeader(new DictionaryViewModel { Key = "a", Value = "2" });

        state.GetHeaders().Should().Equal(new Dictionary<string, string> { ["a"] = "2" });
    }

    [Fact(DisplayName = "GetFrameworkProperties ignores blank keys and keeps the last duplicate")]
    public void GetFrameworkProperties_Tolerant()
    {
        var state = NewState();
        state.AddAdditionalProperty(new DictionaryViewModel { Key = "", Value = "x" });
        state.AddAdditionalProperty(new DictionaryViewModel { Key = "a", Value = "1" });
        state.AddAdditionalProperty(new DictionaryViewModel { Key = "a", Value = "2" });

        state.GetFrameworkProperties().Should().Equal(new Dictionary<string, string> { ["a"] = "2" });
    }
}
