namespace Iris.Contracts.Messaging.Events
{
    public class MessageSent
    {
        public required string Address { get; set; }

        public required string Provider { get; set; }
        
        public string? Endpoint { get; set; }

        public string Message { get; set; } = "";
        
        public Dictionary<string, string>? Headers { get; set; }
        
        public Dictionary<string, string>? Properties { get; set; } 
    }
}

