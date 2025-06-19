using System;
namespace Iris.Contracts.Brokers.Models
{
    public class SupportedProvider
    {
        public required string Name { get; set; }

        private string? _normalized;

        private string NormalizedName
        {
            get
            {
                return _normalized ??= $"{Name.Replace(" ", "")}";
            }
        }

        public string? Product { get; set; }

        public string Icon { get => $"icon-{NormalizedName}"; }

        public override bool Equals(object? obj)
        {
            if (obj is SupportedProvider other)
            {
                return Name == other.Name && Product == other.Product;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Product);
        }

        public override string ToString()
        {
            return $"SupportedProvider: {Name}, {Product}";
        }
    }
}

