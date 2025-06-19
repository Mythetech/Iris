using System;
namespace Iris.Contracts.Subscriptions
{
    public class Subscription
    {
        public bool IsActive { get; set; }

        public bool IsTrial { get; set; }

        public bool IsExpired { get; set; }

        public string? CustomerId { get; set; }
    }
}

