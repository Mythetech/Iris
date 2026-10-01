using FluentAssertions;
using Iris.Desktop.Brokers;

namespace Iris.Desktop.Test.Brokers;

public class SavedConnectionTests
{
    [Fact(DisplayName = "A Google connection keeps its project and credentials choice through a save and a restore")]
    public void Google_fields_survive_a_round_trip()
    {
        var data = new Iris.Contracts.Brokers.Models.ConnectionData
        {
            Provider = "Google",
            ProjectId = "my-project",
            CredentialsPath = "/keys/service-account.json",
            CredentialSource = "CredentialsFile",
        };

        var restored = SavedConnection
            .FromConnectionData("Google", "pubsub.googleapis.com/projects/my-project", data)
            .ToConnectionData();

        restored.ProjectId.Should().Be("my-project");
        restored.CredentialsPath.Should().Be("/keys/service-account.json");
        restored.CredentialSource.Should().Be("CredentialsFile");
    }
}
