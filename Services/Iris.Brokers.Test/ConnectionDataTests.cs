using FluentAssertions;
using Iris.Brokers.Models;
using Xunit;
using ConnectorProviders = Iris.Contracts.Brokers.Models.ConnectorProviders;
using ConnectorTransports = Iris.Contracts.Brokers.Models.ConnectorTransports;

namespace Iris.Brokers.Test;

public class ConnectionDataTests
{
    [Fact]
    public void FromContract_carries_the_google_fields_across()
    {
        var contract = new Iris.Contracts.Brokers.Models.ConnectionData
        {
            Provider = ConnectorProviders.Google,
            Uri = "localhost:8085",
            ProjectId = "my-project",
            CredentialsPath = "/keys/service-account.json",
            CredentialSource = "CredentialsFile",
        };

        var data = ConnectionData.FromContract(contract);

        data.Uri.Should().Be("localhost:8085");
        data.ProjectId.Should().Be("my-project");
        data.CredentialsPath.Should().Be("/keys/service-account.json");
        data.CredentialSource.Should().Be("CredentialsFile");
    }

    [Fact]
    public void Google_is_a_known_provider_with_a_pubsub_transport()
    {
        // These values are persisted with saved connections and matched by the UI's
        // per-provider registry and its logo CSS class, so they are a contract, not a label.
        ConnectorProviders.Google.Should().Be("Google");
        ConnectorTransports.PubSub.Should().Be("PubSub");
        ConnectorProviders.All.Should().Contain(ConnectorProviders.Google);
    }
}
