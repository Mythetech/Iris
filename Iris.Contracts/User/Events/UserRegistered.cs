using System;
namespace Iris.Contracts.User.Events
{
    public record UserRegistered(string Email, Guid TenantId);
}

