using System;
namespace Iris.Brokers
{
    public class EndpointDetails
    {
        public required string Address { get; set; }

        public required string Provider { get; set; }

        public required string Name { get; set; }

        public required string Type { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }
}

