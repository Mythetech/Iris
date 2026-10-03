using Iris.Desktop.Infrastructure;

namespace Iris.Desktop.Brokers;

public class SavedConnection : ILocalEntity
{
    public int Id { get; set; }

    public string Provider { get; set; } = "";

    public string Address { get; set; } = "";

    public string? Uri { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string? ConnectionString { get; set; }

    public string? Region { get; set; }

    public string? ProjectId { get; set; }

    public string? CredentialsPath { get; set; }

    public string? VHost { get; set; }

    public string? AuthMode { get; set; }

    public string? Profile { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public Iris.Brokers.Models.ConnectionData ToConnectionData()
    {
        return new Iris.Brokers.Models.ConnectionData
        {
            Uri = Uri,
            Username = Username,
            Password = Password,
            ConnectionString = ConnectionString,
            Region = Region,
            ProjectId = ProjectId,
            CredentialsPath = CredentialsPath,
            VHost = VHost,
            AuthMode = AuthMode,
            Profile = Profile
        };
    }

    public static SavedConnection FromConnectionData(string provider, string address, Iris.Contracts.Brokers.Models.ConnectionData data)
    {
        return new SavedConnection
        {
            Provider = provider,
            Address = address,
            Uri = data.Uri,
            Username = data.Username,
            Password = data.Password,
            ConnectionString = data.ConnectionString,
            Region = data.Region,
            ProjectId = data.ProjectId,
            CredentialsPath = data.CredentialsPath,
            VHost = data.VHost,
            AuthMode = data.AuthMode,
            Profile = data.Profile
        };
    }
}
