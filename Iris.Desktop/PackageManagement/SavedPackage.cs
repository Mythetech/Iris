using Iris.Desktop.Infrastructure;

namespace Iris.Desktop.PackageManagement;

public class SavedPackage : ILocalEntity
{
    public int Id { get; set; }

    public string FilePath { get; set; } = "";

    public string AssemblyName { get; set; } = "";

    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.Now;
}
