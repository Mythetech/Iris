using System;
namespace Iris.Contracts.User
{
    public static class RegisterUser
    {
        public record RegisterUserRequest(string Email, string Password, Guid? SubscriptionKey = null, bool FreeTrial = false);

        public record RegisterUserResponse(bool Success);
    }
}

