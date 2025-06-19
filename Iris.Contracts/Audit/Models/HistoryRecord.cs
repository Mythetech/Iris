namespace Iris.Contracts.Audit.Models;

public class HistoryRecord
{
    public string Action { get; set; }
    
    public string? EventAction { get; set; }
    
    public Dictionary<string, object>? EventParameters { get; set; }
    
    public string? Source { get; set; }
    
    public string? Target { get; set; }
    
    public string? Details { get; set; }
    
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
}