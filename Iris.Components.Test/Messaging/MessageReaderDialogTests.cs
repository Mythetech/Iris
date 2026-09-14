using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Iris.Components.Brokers;
using Iris.Components.Messaging;
using Iris.Contracts.Brokers.Models;
using Iris.Contracts.Messaging.Models;
using Iris.Contracts.Results;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;
using Xunit;

namespace Iris.Components.Test.Messaging;

public class MessageReaderDialogTests : IrisTestContext
{
    private readonly IMessageService _messages = Substitute.For<IMessageService>();
    private readonly BrokerOperationSettings _settings = new();

    private static readonly EndpointDetails Queue = new()
    {
        Name = "orders",
        Address = "http://127.0.0.1:15672",
        Provider = "rabbitmq",
        Type = "Queue",
    };

    public MessageReaderDialogTests()
    {
        _messages.ReceiveMessagesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
            .Returns(new Success<IReadOnlyList<ReceivedMessageDto>>([]));

        Services.AddSingleton(_messages);
        Services.AddSingleton(_settings);
    }

    // MudDialog renders through the provider rather than in place, so the reader produces
    // no markup at all unless it is shown the way the app shows it.
    private async Task<IRenderedComponent<MudDialogProvider>> ShowReaderAsync()
    {
        AddPopoverProvider();

        var provider = Render<MudDialogProvider>();
        var dialogs = Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters
        {
            { nameof(MessageReaderDialog.Endpoint), Queue },
            { nameof(MessageReaderDialog.Capabilities), new ReaderCapabilitiesDto(false, true, false, false, 0, 10) },
        };

        await provider.InvokeAsync(() => dialogs.ShowAsync<MessageReaderDialog>("Read orders", parameters));

        return provider;
    }

    private static IElement ButtonSaying(IRenderedComponent<MudDialogProvider> provider, string text) =>
        provider.FindAll("button").Single(b => b.TextContent.Contains(text, StringComparison.Ordinal));

    [Fact(DisplayName = "Confirms before a destructive receive when the setting is on")]
    public async Task Confirms_when_the_setting_is_on()
    {
        _settings.RequireConfirmationOnDestructiveRead = true;

        var provider = await ShowReaderAsync();

        // Held rather than awaited inline: the click handler is sitting on the confirm
        // dialog's result, so it only completes once the prompt below is answered.
        var click = ButtonSaying(provider, "Receive").ClickAsync(new());

        provider.Markup.Should().Contain("Confirm receive");

        await ButtonSaying(provider, "Cancel").ClickAsync(new());
        await click;

        await _messages.DidNotReceive()
            .ReceiveMessagesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
    }

    // Timed out rather than left to hang: if the guard is ever removed, the click
    // handler blocks on a confirm dialog nobody answers, and a hung CI run is a worse
    // signal than a failed assertion.
    [Fact(DisplayName = "Receives without confirming when the setting is off", Timeout = 15000)]
    public async Task Skips_the_prompt_when_the_setting_is_off()
    {
        // The setting shipped in the Settings dialog and was read nowhere in the repo, so
        // turning it off changed nothing and the prompt appeared on every receive.
        _settings.RequireConfirmationOnDestructiveRead = false;

        var provider = await ShowReaderAsync();

        await ButtonSaying(provider, "Receive").ClickAsync(new());

        provider.Markup.Should().NotContain("Confirm receive");
        await _messages.Received(1).ReceiveMessagesAsync(Queue.Address, Queue.Name, Arg.Any<int>());
    }
}
