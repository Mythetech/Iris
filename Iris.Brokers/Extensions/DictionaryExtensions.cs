namespace Iris.Brokers.Extensions;

public static class DictionaryExtensions
{
    public static IReadOnlyDictionary<string, object> ToReadOnly(this Dictionary<string, string> d)
    {
        return d.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value).AsReadOnly();

    }
}