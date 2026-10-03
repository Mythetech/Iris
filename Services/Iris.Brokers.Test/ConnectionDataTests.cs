using FluentAssertions;
using Iris.Brokers.Models;
using Xunit;
using ConnectorProviders = Iris.Contracts.Brokers.Models.ConnectorProviders;
using ConnectorTransports = Iris.Contracts.Brokers.Models.ConnectorTransports;
using ContractConnectionData = Iris.Contracts.Brokers.Models.ConnectionData;

namespace Iris.Brokers.Test;

public class ConnectionDataTests
{
    public static TheoryData<string> BrokerProperties()
    {
        var names = new TheoryData<string>();

        foreach (var property in typeof(ConnectionData).GetProperties().Where(p => p.CanWrite))
            names.Add(property.Name);

        return names;
    }

    [Theory(DisplayName = "FromContract carries every field the broker reads")]
    [MemberData(nameof(BrokerProperties))]
    public void Carries_every_broker_field(string propertyName)
    {
        var contractProperty = typeof(ContractConnectionData).GetProperty(propertyName);
        contractProperty.Should().NotBeNull($"the contract has to carry {propertyName} for the broker to receive it");

        var contract = new ContractConnectionData();
        contractProperty!.SetValue(contract, $"value-of-{propertyName}");

        var mapped = ConnectionData.FromContract(contract);

        typeof(ConnectionData).GetProperty(propertyName)!.GetValue(mapped)
            .Should().Be($"value-of-{propertyName}");
    }

    [Fact]
    public void FromContract_carries_the_google_fields_across()
    {
        var contract = new ContractConnectionData
        {
            Provider = ConnectorProviders.Google,
            Uri = "localhost:8085",
            ProjectId = "my-project",
            CredentialsPath = "/keys/service-account.json",
            AuthMode = "CredentialsFile",
        };

        var data = ConnectionData.FromContract(contract);

        data.Uri.Should().Be("localhost:8085");
        data.ProjectId.Should().Be("my-project");
        data.CredentialsPath.Should().Be("/keys/service-account.json");
        data.AuthMode.Should().Be("CredentialsFile");
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
