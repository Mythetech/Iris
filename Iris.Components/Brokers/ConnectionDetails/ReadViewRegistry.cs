namespace Iris.Components.Brokers.ConnectionDetails;

public sealed class ReadViewRegistry : Dictionary<string, Type>
{
    public ReadViewRegistry() : base(StringComparer.OrdinalIgnoreCase) { }
}
