using Iris.Contracts.Messaging.Frameworks;
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
        var frameworkName = context.Framework ?? _messageState.SelectedFramework;

        var validation = context.IsolateFromMessageState
            ? null
            : ValidateRequiredInputs(frameworkName);
        if (validation is not null)
        {
            progress?.Report(validation);
            return validation;
        }

        Result<bool>? lastResponse = null;

        int repeat = context.Repeat ?? _messageState.Repeat;

        if (repeat > 0)
        {
            // Match the original behaviour: repeat + 1 total sends (for the 0th message)
            var total = repeat + 1;
            for (int i = 0; i < total; i++)
            {
                _messageState.SetRepeatText($"{total - i} remaining");
                lastResponse = await SendOnceAsync(frameworkName, context, cancellationToken);
                progress?.Report(lastResponse);
            }
            _messageState.SetRepeatText("");
        }
        else
        {
            lastResponse = await SendOnceAsync(frameworkName, context, cancellationToken);
            progress?.Report(lastResponse);
        }

        return lastResponse!;
    }

    private Result<bool>? ValidateRequiredInputs(string? frameworkName)
    {
        var descriptor = _messageState.SelectedFrameworkDescriptor?.Name == frameworkName
            ? _messageState.SelectedFrameworkDescriptor
            : _messageState.AvailableFrameworks.FirstOrDefault(f => f.Name == frameworkName);

        if (descriptor is null)
            return null;

        var properties = _messageState.GetFrameworkProperties();

        var missing = descriptor.Inputs.FirstOrDefault(input =>
            input.Required && string.IsNullOrWhiteSpace(properties.GetValueOrDefault(input.Key)));

        return missing is null
            ? null
            : new Failure<bool>($"{missing.Label} is required for {descriptor.Name}.");
    }

    private async Task<Result<bool>> SendOnceAsync(string? frameworkName, SendContext context, CancellationToken cancellationToken)
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

        // An isolated send composes its own message: the ambient properties grid belongs to
        // the messaging page and must not ride along.
        var properties = context.IsolateFromMessageState
            ? new Dictionary<string, string>()
            : _messageState.GetFrameworkProperties();

        if (!string.IsNullOrWhiteSpace(context.MessageTypeOverride)
            && string.IsNullOrWhiteSpace(properties.GetValueOrDefault(FrameworkInputs.TypeName)))
        {
            properties[FrameworkInputs.TypeName] = context.MessageTypeOverride;
        }

        return await _messageService.SendMessageAsync(
            context.Endpoint?.Name!,
            context.Json,
            context.Provider?.Address,
            frameworkName,
            properties,
            context.Headers ?? _messageState.GetHeaders());
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
