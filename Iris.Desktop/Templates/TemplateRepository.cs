using Iris.Desktop.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Iris.Desktop.Templates;

public class TemplateRepository : IRepository
{
    private readonly IrisLiteDbContext _dbContext;
    private readonly ILogger<TemplateRepository> _logger;

    public string DbKey { get; } = "templates";

    public TemplateRepository(IrisLiteDbContext dbContext, ILogger<TemplateRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public List<PersistentTemplate> GetAll()
    {
        try
        {
            return _dbContext.GetCollection<PersistentTemplate>(DbKey)
                .FindAll()
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read templates from LiteDB");
            return [];
        }
    }

    public void Save(PersistentTemplate template)
    {
        var collection = _dbContext.GetCollection<PersistentTemplate>(DbKey);

        var existing = collection.FindOne(x => x.TemplateId == template.TemplateId);

        if (existing != null)
        {
            template.Id = existing.Id;
            collection.Update(template);
        }
        else
        {
            collection.Insert(template);
        }
    }

    public void Delete(Guid templateId)
    {
        var collection = _dbContext.GetCollection<PersistentTemplate>(DbKey);
        collection.DeleteMany(x => x.TemplateId == templateId);
    }
}
