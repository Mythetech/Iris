using System;
using Iris.Api.Services.Integrations;

namespace Iris.Integration.Tests.Infrastructure
{
    public class EmulatedKeyVaultService : ISecretService
    {
        private Dictionary<Guid, string> Secrets { get; set; } = new();

        public Task<Guid> CreateSecretAsync(string value)
        {
            var id = Guid.NewGuid();

            Secrets[id] = value;

            return Task.FromResult(id);
        }

        public Task<string> GetSecretAsync(Guid key)
        {
            if (Secrets.TryGetValue(key, out string? value))
            {
                return Task.FromResult(value);
            }

            return Task.FromResult("");
        }

        public Task RemoveSecretIfExistsAsync(Guid key)
        {
            Secrets.Remove(key);

            return Task.CompletedTask;
        }
    }
}

