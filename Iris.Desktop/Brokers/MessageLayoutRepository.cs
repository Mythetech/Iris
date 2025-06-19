using Iris.Components.Messaging;
using Iris.Components.Shared.DynamicTabs;
using Iris.Desktop.Infrastructure;

namespace Iris.Desktop.Brokers;

public class MessageLayoutRepository : IMessagingLayoutService, IRepository
{
    private readonly IrisLiteDbContext _dbContext;
    
    public string DbKey { get; } = "iris_message_layout";

    public MessageLayoutRepository(IrisLiteDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public Task SaveLayoutAsync(List<DynamicTabView> layout)
    {
        var collection = _dbContext.GetCollection<PersistentMessagingLayout>(DbKey);
        var existing = collection.FindAll().FirstOrDefault();

        if (existing == null)
        {
            collection.Insert(new PersistentMessagingLayout()
            {
                Id = 0,
                Layout = layout.Select(l => l.ToSerializedModel()).ToList(),
            });
        }
        else
        {
            existing.Layout = layout.Select(x => x.ToSerializedModel()).ToList();
            collection.Update(existing);
        }
        
        return Task.CompletedTask;
    }

    public Task<List<DynamicTabView>?> LoadLayoutAsync()
    {
        try
        {
            return Task.FromResult(_dbContext.GetCollection<PersistentMessagingLayout>(DbKey).FindAll().FirstOrDefault()
                ?.Layout.Select(x => x.ToModel()).ToList() ?? []);
        }
        catch
        {
            return default!;
        }

    }
}
