using System;
namespace Iris.Contracts.Assemblies.Models
{
    public class PackageSource
    {
        public string Name { get; set; } = "";

        public string FeedUri { get; set; } = "";

        public string Source { get; set; } = "";

        public bool Protected { get; set; }
    }
}

