using Iris.Contracts.Messaging.Models;
using Iris.Contracts.Results;

namespace Iris.Components.Messaging
{
    public interface IMessageService
    {
        public Task<string> GetMessageStructureAsync(string messageType);

        public Task<Result<bool>> SendMessageAsync(string messageType, string messageJson, string? address, string? framework = default, Dictionary<string, string>? properties = default, Dictionary<string, string>? headers = default);

        public Task<Result<bool>> SendMessageAsync(Message message);

        /// <summary>
        /// Non-destructively read up to <paramref name="count"/> messages from the main queue.
        /// Returns a failure result if the broker does not implement non-destructive peek.
        /// </summary>
        public Task<Result<IReadOnlyList<ReceivedMessageDto>>> PeekMessagesAsync(
            string address,
            string endpointName,
            int count,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Destructively consume up to <paramref name="count"/> messages from the main queue.
        /// This is irrevocable — messages are auto-acknowledged before return.
        /// Returns a failure result if the broker does not implement destructive receive.
        /// </summary>
        public Task<Result<IReadOnlyList<ReceivedMessageDto>>> ReceiveMessagesAsync(
            string address,
            string endpointName,
            int count,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Non-destructively read up to <paramref name="count"/> messages from the dead-letter sub-queue.
        /// Returns a failure result if the broker does not expose a peekable DLQ.
        /// </summary>
        public Task<Result<IReadOnlyList<ReceivedMessageDto>>> PeekDeadLetterMessagesAsync(
            string address,
            string endpointName,
            int count,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Destructively consume up to <paramref name="count"/> messages from the dead-letter sub-queue.
        /// This is irrevocable — messages are auto-acknowledged before return.
        /// Returns a failure result if the broker does not expose a receivable DLQ.
        /// </summary>
        public Task<Result<IReadOnlyList<ReceivedMessageDto>>> ReceiveDeadLetterMessagesAsync(
            string address,
            string endpointName,
            int count,
            CancellationToken cancellationToken = default);
    }
}
