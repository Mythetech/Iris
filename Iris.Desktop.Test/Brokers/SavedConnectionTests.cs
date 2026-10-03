using FluentAssertions;
using Iris.Desktop.Brokers;
using BrokerConnectionData = Iris.Brokers.Models.ConnectionData;
using ContractConnectionData = Iris.Contracts.Brokers.Models.ConnectionData;

namespace Iris.Desktop.Test.Brokers;

/// <summary>
/// A connection is described by three classes: the contract the UI fills in, the model the
/// broker reads, and the copy on disk. A field added to the first two and forgotten on the
/// third does not fail a build; the connection simply restores without it. VHost went
/// missing that way. Walking the broker model by reflection means the next field cannot.
/// </summary>
public class SavedConnectionTests
{
    public static TheoryData<string> BrokerProperties()
    {
        var names = new TheoryData<string>();

        foreach (var property in typeof(BrokerConnectionData).GetProperties().Where(p => p.CanWrite))
            names.Add(property.Name);

        return names;
    }

    [Theory(DisplayName = "A saved connection restores every field the broker reads")]
    [MemberData(nameof(BrokerProperties))]
    public void Restores_every_broker_field(string propertyName)
    {
        var contractProperty = typeof(ContractConnectionData).GetProperty(propertyName);
        contractProperty.Should().NotBeNull($"the contract has to carry {propertyName} for the broker to receive it");

        var contract = new ContractConnectionData();
        contractProperty!.SetValue(contract, $"value-of-{propertyName}");

        var restored = SavedConnection.FromConnectionData("Amazon", "address", contract).ToConnectionData();

        typeof(BrokerConnectionData).GetProperty(propertyName)!.GetValue(restored)
            .Should().Be($"value-of-{propertyName}");
    }

    [Fact(DisplayName = "A Google connection keeps its project and credentials choice through a save and a restore")]
    public void Google_fields_survive_a_round_trip()
    {
        var data = new ContractConnectionData
        {
            Provider = "Google",
            ProjectId = "my-project",
            CredentialsPath = "/keys/service-account.json",
            AuthMode = "CredentialsFile",
        };

        var restored = SavedConnection
            .FromConnectionData("Google", "pubsub.googleapis.com/projects/my-project", data)
            .ToConnectionData();

        restored.ProjectId.Should().Be("my-project");
        restored.CredentialsPath.Should().Be("/keys/service-account.json");
        restored.AuthMode.Should().Be("CredentialsFile");
    }
}
