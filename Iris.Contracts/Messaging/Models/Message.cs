namespace Iris.Contracts.Messaging.Models;

public class Message
{
    public required string MessageType { get; set; }
    
    public string? Data { get; set; }
    
    public required string Address { get; set; }
    
    public string? Framework { get; set; } = default!;
    
    public Dictionary<string, string> Headers { get; set; } = new();
    
    public Dictionary<string, string>? Properties { get; set; } = default!;
    
    public DateTimeOffset Timestamp { get; private set; } = DateTimeOffset.UtcNow;

    public bool GenerateIrisHeaders { get; set; } = true;
}