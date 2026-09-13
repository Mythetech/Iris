using Iris.Contracts.Messaging.Frameworks;

namespace Iris.Brokers.Frameworks;

internal static class CommonInputs
{
    public static FrameworkInput TypeName(string feeds) => new(
        FrameworkInputs.TypeName,
        "Type name",
        $"Fully qualified message type, for example MyApp.Messages.OrderPlaced. Feeds {feeds}. Blank means the endpoint name.");

    public static FrameworkInput AssemblyName(string feeds, bool required = false) => new(
        FrameworkInputs.AssemblyName,
        "Assembly name",
        $"Assembly that defines the type, for example MyApp.Messages. Feeds {feeds}. Filled in automatically when the type comes from a loaded package.",
        required);
}
