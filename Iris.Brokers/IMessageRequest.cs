namespace Iris.Brokers;

public interface IMessageRequest
{
        string MessageType { get; set; }
        
        string? MessageFullyQualifiedName { get; set; }

        string? MessageAssemblyName { get; set; }

        string Json { get; set; }
        
        string? Framework { get; set; }
        
        Dictionary<string, string> Properties { get; set; }
        
        Dictionary<string, string> Headers { get; set; }
}