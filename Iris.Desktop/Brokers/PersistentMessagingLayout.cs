using Iris.Components.Shared.DynamicTabs;
using Iris.Desktop.Infrastructure;

namespace Iris.Desktop.Brokers;

public class PersistentMessagingLayout : ILocalEntity
{
    public int Id { get; set; }
    
    public List<SerializedDynamicTabModel> Layout { get; set; }
}

