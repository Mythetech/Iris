using System;
using System.Text.RegularExpressions;

namespace Iris.Contracts.Brokers.Models
{
    public partial class Provider
    {
        public string Name { get; set; } = "";
        
        public int Endpoints { get; set; }

        public string Address { get; set; } = "";

        public string Transport { get; set; } = "";

        public string TransportDisplay
        {
            get
            {
                if (string.IsNullOrEmpty(Transport) || Transport.Equals(Name, StringComparison.OrdinalIgnoreCase))
                {
                    return "";
                }

                var words = SplitCamelCase(Transport);
                var display = string.Join(" ", words.Select(word => char.ToUpper(word[0]) + word[1..]));

                if (display.StartsWith(Name, StringComparison.OrdinalIgnoreCase))
                {
                    display = display[Name.Length..].TrimStart();
                }

                return display;
            }
        }

        private static string[] SplitCamelCase(string source)
        {
            return CamelCaseRegex().Split(source);
        }
        
        private static readonly char[] _separator = new[] { ' ' };

        public override string ToString()
        {
            return Address;
        }

        [GeneratedRegex(@"(?<!^)(?=[A-Z])")]
        private static partial Regex CamelCaseRegex();
    }
}

