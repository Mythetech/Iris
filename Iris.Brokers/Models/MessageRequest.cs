using Iris.Brokers.Frameworks;

namespace Iris.Brokers.Models;

public class MessageRequest : IMessageRequest
{
    public required string MessageType { get; set; }

    public string? MessageFullyQualifiedName { get; set; }

    public string? MessageAssemblyName { get; set; }

    public required string Json { get; set; }

    public string? Framework { get; set; }

    public Dictionary<string, string> Properties { get; set; } = new();

    public Dictionary<string, string> Headers { get; set; } = new();

    public void WrapMessage(IFrameworkProvider frameworkProvider)
    {
        var framework = frameworkProvider.GetFramework(Framework);
        WrapMessage(framework);
    }

    public void WrapMessage(IFramework framework)
    {
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
            Headers = headers ?? new Dictionary<string, string>(),
            Properties = properties ?? new Dictionary<string, string>(),
        };

        if (generateIrisHeaders)
        {
            message.Headers["iris-key"] = Guid.NewGuid().ToString();
        }

        return message;
    }
}