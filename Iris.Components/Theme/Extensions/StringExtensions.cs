namespace Iris.Cloud.Client.Shared.Extensions
{
    public static class StringExtensions
    {
        public static bool IsNullOrWhitespace(this string str)
        {
            return string.IsNullOrWhiteSpace(str);
        }
    }
}

