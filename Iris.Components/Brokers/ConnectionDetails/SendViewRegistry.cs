namespace Iris.Components.Brokers.ConnectionDetails;

public sealed class SendViewRegistry : Dictionary<string, Type>
{
    public SendViewRegistry() : base(StringComparer.OrdinalIgnoreCase) { }
}
