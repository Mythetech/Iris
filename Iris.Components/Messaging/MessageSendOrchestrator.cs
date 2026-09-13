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

        int repeat = context.Repeat ?? _messageState.Repeat;

        if (repeat > 0)
        {
            // Match the original behaviour: repeat + 1 total sends (for the 0th message)
            var total = repeat + 1;
            for (int i = 0; i < total; i++)
            {
                _messageState.SetRepeatText($"{total - i} remaining");
                lastResponse = await SendOnceAsync(messageType, context, cancellationToken);
                progress?.Report(lastResponse);
            }
            _messageState.SetRepeatText("");
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
        int delay = context.Delay ?? _messageState.Delay;

        if (delay > 0)
        {
            await HandleDelayAsync(delay, cancellationToken);
        }

        if (!context.IsolateFromMessageState)
        {
            _messageState.SetEndpointMetadata(context.Endpoint);
        }

        return await _messageService.SendMessageAsync(
            messageType!,
            context.Json,
            context.Provider?.Address,
            context.Framework ?? _messageState.SelectedFramework,
            _messageState.GetFrameworkProperties(),
            context.Headers ?? _messageState.Headers);
    }

    private async Task HandleDelayAsync(int delay, CancellationToken cancellationToken)
    {
        for (int i = 0; i < delay; i++)
        {
            int remaining = delay - (i + 1);
            _messageState.SetDelayText(remaining > 0 ? $"Sending in {remaining}..." : "Sending...");
            await Task.Delay(1000, cancellationToken);
        }

        _messageState.SetDelayText("");
    }
}
