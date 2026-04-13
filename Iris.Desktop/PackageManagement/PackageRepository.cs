using Iris.Desktop.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Iris.Desktop.PackageManagement;

public class PackageRepository : IRepository
{
    private readonly IrisLiteDbContext _dbContext;
    private readonly ILogger<PackageRepository> _logger;

    public string DbKey { get; } = "packages";

    public PackageRepository(IrisLiteDbContext dbContext, ILogger<PackageRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public List<SavedPackage> GetAll()
    {
        try
        {
            return _dbContext.GetCollection<SavedPackage>(DbKey)
                .FindAll()
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read saved packages from LiteDB");
            return [];
        }
    }

    public void Save(SavedPackage package)
    {
        var collection = _dbContext.GetCollection<SavedPackage>(DbKey);

        var existing = collection.FindOne(x => x.FilePath == package.FilePath);

        if (existing != null)
        {
            package.Id = existing.Id;
            collection.Update(package);
        }
        else
        {
            collection.Insert(package);
        }
    }

    public void Delete(string filePath)
    {
        var collection = _dbContext.GetCollection<SavedPackage>(DbKey);
        collection.DeleteMany(x => x.FilePath == filePath);
    }

    public void DeleteById(int id)
    {
        var collection = _dbContext.GetCollection<SavedPackage>(DbKey);
        collection.Delete(id);
    }
}
