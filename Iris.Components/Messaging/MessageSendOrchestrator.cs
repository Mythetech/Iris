using Iris.Contracts.Results;

namespace Iris.Components.Messaging;

public sealed class MessageSendOrchestrator : IMessageSendOrchestrator
{
    private readonly IMessageService _messageService;
    private readonly MessageState _messageState;

    public MessageSendOrchestrator(IMessageService messageService, MessageState messageState)
    {
        _messageService = messageService;
        _messageState = messageState;
    }

    public async Task<Result<bool>> SendAsync(
        SendContext context,
        IProgress<Result<bool>>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _messageState.GetFrameworkProperties().TryGetValue("MessageType", out string? frameworkOverride);

        string? messageType = !string.IsNullOrWhiteSpace(frameworkOverride)
            ? frameworkOverride
            : (context.MessageTypeOverride ?? context.Endpoint?.Name);

        Result<bool>? lastResponse = null;

        if (_messageState.Repeat > 0)
        {
            // Match the original behaviour: repeat + 1 total sends (for the 0th message)
            var total = _messageState.Repeat + 1;
            for (int i = 0; i < total; i++)
            {
                _messageState.RepeatText = $"{total - i} remaining";
                _messageState.NotifyStateChanged();
                lastResponse = await SendOnceAsync(messageType, context, cancellationToken);
                progress?.Report(lastResponse);
            }
            _messageState.RepeatText = "";
            _messageState.NotifyStateChanged();
        }
        else
        {
            lastResponse = await SendOnceAsync(messageType, context, cancellationToken);
            progress?.Report(lastResponse);
        }

        return lastResponse!;
    }

    private async Task<Result<bool>> SendOnceAsync(string? messageType, SendContext context, CancellationToken cancellationToken)
    {
        if (_messageState.Delay > 0)
        {
            await HandleDelayAsync(_messageState.Delay, cancellationToken);
        }

        _messageState.SetEndpointMetadata(context.Endpoint);

        return await _messageService.SendMessageAsync(
            messageType!,
            context.Json,
            context.Provider?.Address,
            _messageState.SelectedFramework,
            _messageState.GetFrameworkProperties(),
            _messageState.Headers);
    }

    private async Task HandleDelayAsync(int delay, CancellationToken cancellationToken)
    {
        for (int i = 0; i < delay; i++)
        {
            int remaining = delay - (i + 1);
            _messageState.DelayText = remaining > 0 ? $"Sending in {remaining}..." : "Sending...";
            _messageState.NotifyStateChanged();
            await Task.Delay(1000, cancellationToken);
        }

        _messageState.DelayText = "";
        _messageState.NotifyStateChanged();
    }
}
