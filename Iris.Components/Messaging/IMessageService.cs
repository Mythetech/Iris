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

        /// <summary>
        /// Non-destructively read up to <paramref name="count"/> messages from the endpoint.
        /// </summary>
        public Task<Result<IReadOnlyList<ReceivedMessageDto>>> PeekMessagesAsync(
            string address,
            string endpointName,
            int count,
            ReadSource source = ReadSource.Main,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Destructively consume up to <paramref name="count"/> messages from the endpoint.
        /// This is irrevocable — messages are auto-acknowledged before return.
        /// </summary>
        public Task<Result<IReadOnlyList<ReceivedMessageDto>>> ReceiveMessagesAsync(
            string address,
            string endpointName,
            int count,
            ReadSource source = ReadSource.Main,
            CancellationToken cancellationToken = default);
    }
}