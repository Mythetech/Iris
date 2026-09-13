namespace Iris.Contracts.Messaging.Frameworks;

public interface IFrameworkCatalog
{
    /// <summary>
    /// Every registered framework. With a provider address, each descriptor also says
    /// whether that provider can carry the framework and why not; without one, all are
    /// reported as supported. <paramref name="userHeaderCount"/> is how many headers the
    /// user will send alongside the framework's own, including the iris key when enabled.
    /// </summary>
    Task<IReadOnlyList<FrameworkDescriptor>> GetFrameworksAsync(string? providerAddress, int userHeaderCount);
}
