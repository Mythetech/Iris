using FluentAssertions;
using Iris.Brokers.Frameworks;
using Iris.Brokers.Models;
using Xunit;

namespace Iris.Brokers.Test.Frameworks;

public class MassTransitAdapterTests
{
    [Fact(DisplayName = "Declares only body keys and writes nothing outside the body")]
    public void Keys_AreBodyOnly()
    {
        var adapter = new MassTransitAdapter();

        adapter.Keys.Should().NotBeEmpty();
        adapter.Keys.Should().OnlyContain(k => k.Location == KeyLocation.Body);
        FrameworkKeyAssertions.AssertKeysMatchWrite(adapter,
            MessageRequest.Create("Greeting", "{\"id\":1}", false, "MyApp.Messages.Greeting"));
    }

    [Fact(DisplayName = "Message type urn uses the fully qualified name")]
    public void CreateWrappedMessage_UrnFromFullName()
    {
        var request = MessageRequest.Create("Greeting", "{\"id\":1}", false, "MyApp.Messages.Greeting");

        var body = new MassTransitAdapter().CreateWrappedMessage(request);

        body.Should().Contain("urn:message:MyApp.Messages.Greeting");
    }
}
