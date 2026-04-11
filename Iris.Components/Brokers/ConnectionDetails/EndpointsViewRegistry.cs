namespace Iris.Components.Brokers.ConnectionDetails;

/// <summary>
/// Maps normalized provider name (lower-case, no spaces) to the
/// component Type used to render the Endpoints tab for that broker.
/// </summary>
public sealed class EndpointsViewRegistry : Dictionary<string, Type>
{
    public EndpointsViewRegistry() : base(StringComparer.OrdinalIgnoreCase) { }
}
