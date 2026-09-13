using Iris.Brokers.Frameworks;

namespace Iris.Brokers.Models;

public class MessageRequest : IMessageRequest
{
    private readonly Dictionary<string, HeaderDataType> _headerTypes = new();

    public required string MessageType { get; set; }

    public string? MessageFullyQualifiedName { get; set; }

    public string? MessageAssemblyName { get; set; }

    public required string Json { get; set; }

    public string? Framework { get; set; }

    public Dictionary<string, string> Properties { get; set; } = new();

    public Dictionary<string, string> Headers { get; set; } = new();

    public TransportProperties TransportProperties { get; set; } = new();

    public IReadOnlyDictionary<string, HeaderDataType> HeaderTypes => _headerTypes;

    public HeaderDataType HeaderTypeOf(string key)
        => _headerTypes.TryGetValue(key, out var type) ? type : HeaderDataType.String;

    internal void DeclareHeaderType(string key, HeaderDataType type) => _headerTypes[key] = type;

    public void WrapMessage(IFrameworkProvider frameworkProvider)
    {
        var framework = frameworkProvider.GetFramework(Framework!);
        WrapMessage(framework!);
    }

    public void WrapMessage(IFramework framework)
    {
        foreach (var key in framework.Keys.Where(k => k.Location == KeyLocation.Header))
        {
            DeclareHeaderType(key.Name, key.DataType);
        }

        Json = framework.CreateWrappedMessage(this);
    }

    public static MessageRequest Create(string messageType,
        string json,
        bool generateIrisHeaders,
        string? messageFullyQualifiedName = default,
        string? framework = default,
        Dictionary<string, string>? headers = default,
        Dictionary<string, string>? properties = default,
        string? messageAssemblyName = default)
    {
        var message = new MessageRequest()
        {
            MessageType = messageType,
            MessageFullyQualifiedName = messageFullyQualifiedName,
            MessageAssemblyName = messageAssemblyName,
            Json = json,
            Framework = framework,
            Headers = headers is null ? new Dictionary<string, string>() : new Dictionary<string, string>(headers),
            Properties = properties is null ? new Dictionary<string, string>() : new Dictionary<string, string>(properties),
        };

        if (generateIrisHeaders)
        {
            message.Headers["iris-key"] = Guid.NewGuid().ToString();
        }

        return message;
    }
}