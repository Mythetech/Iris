using System;
namespace Iris.Brokers.Models
{
    public class Provider
    {
        public required string Name { get; set; }

        public int Endpoints { get; set; }

        public required string Address { get; set; }
    }
}

