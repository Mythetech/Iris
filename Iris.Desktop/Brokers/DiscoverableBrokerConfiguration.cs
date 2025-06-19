
using Iris.Brokers.Models;
using Iris.Desktop.Infrastructure;

namespace Iris.Desktop.Brokers;

public class DiscoverableBrokerConfiguration : ILocalEntity
{
    public ConnectionData Data { get; set; } = default!;
    
    public bool Enabled { get; set; }
    
    public int Id { get; set; }
}