using System;
using Iris.Api.Infrastructure.Account;

namespace Iris.Integration.Tests.Infrastructure
{
    public class TestAccountContext : IAccountContext
    {
        public Guid TenantId { get; set; }
        public Guid UserId { get; set; }
        public string User { get; set; }
        public bool Anonymous { get; set; } = false;

        public TestAccountContext(Guid? tenantId, Guid? userId, string user)
        {
            TenantId = tenantId ?? Guid.NewGuid();
            UserId = userId ?? Guid.NewGuid();
            User = user;
        }
    }
}

