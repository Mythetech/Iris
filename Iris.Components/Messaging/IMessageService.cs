using Iris.Contracts.Messaging.Models;
using Iris.Contracts.Results;

namespace Iris.Components.Messaging
{
    public interface IMessageService
    {
        public Task<string> GetMessageStructureAsync(string messageType);

        public Task<string> CreateMessageDataAsync(string messageType);

        public Task<Result<bool>> SendMessageAsync(string messageType, string messageJson, string? address, string? framework = default, Dictionary<string, string>? properties = default, Dictionary<string, string>? headers = default);

        public Task<Result<bool>> SendMessageAsync(Message message);
    }
}