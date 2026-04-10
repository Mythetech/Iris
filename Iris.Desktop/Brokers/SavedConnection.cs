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

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public Iris.Brokers.Models.ConnectionData ToConnectionData()
    {
        return new Iris.Brokers.Models.ConnectionData
        {
            Uri = Uri,
            Username = Username,
            Password = Password,
            ConnectionString = ConnectionString,
            Region = Region
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
            Region = data.Region
        };
    }
}
