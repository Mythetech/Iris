using Iris.Desktop.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace Iris.Desktop.Test;

/// <summary>
/// A real LiteDB file in a temp directory, torn down with the test. These round trips are
/// worth running against the actual engine rather than a substitute: the defects they
/// cover (a missing index, an ignored page size, unbounded growth) only exist at the
/// storage layer, and a mocked repository would report success for all three.
/// </summary>
public sealed class TempDatabase : IDisposable
{
    private readonly string _directory;

    public TempDatabase()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"iris-db-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);

        DatabasePath = Path.Combine(_directory, "IrisDb.db");
        Context = new IrisLiteDbContext(NullLogger<IrisLiteDbContext>.Instance, DatabasePath);
    }

    public string DatabasePath { get; }

    public IrisLiteDbContext Context { get; }

    public void Dispose()
    {
        Context.Dispose();

        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A file the engine has not finished releasing is not worth failing a test over.
        }
    }
}
