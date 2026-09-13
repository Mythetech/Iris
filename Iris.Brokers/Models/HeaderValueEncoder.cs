using System.Globalization;

namespace Iris.Brokers.Models;

/// <summary>
/// Turns the string a header row holds into the native value a carrier sends.
/// A value that does not parse as its declared type is sent as the string it was,
/// so a typo never blocks a send; the consumer sees exactly what the user typed.
/// </summary>
public static class HeaderValueEncoder
{
    public static object Encode(string value, HeaderDataType type) => type switch
    {
        HeaderDataType.Integer when long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) => l,
        HeaderDataType.Boolean when bool.TryParse(value, out var b) => b,
        HeaderDataType.Timestamp when DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d) => d,
        _ => value,
    };
}
