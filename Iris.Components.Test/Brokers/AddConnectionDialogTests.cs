using Bunit;
using FluentAssertions;
using Iris.Components.Brokers;
using Iris.Components.Infrastructure;
using Iris.Contracts.Brokers.Models;
using Iris.Contracts.Brokers.Models.Amazon;
using Iris.Contracts.Results;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;
using CreateConnectionResponse = Iris.Contracts.Brokers.Endpoints.CreateConnection.CreateConnectionResponse;

namespace Iris.Components.Test.Brokers
{
    /// <summary>
    /// The dialog driven through MudDialogProvider, the way every entry point opens it. Two of
    /// those entry points throw the dialog's result away, so anything the user has to be told
    /// about a failed connection has to be said inside the dialog itself.
    /// </summary>
    public class AddConnectionDialogTests : IrisTestContext
    {
        private static readonly SupportedProvider Amazon = new() { Name = "Amazon" };
        private static readonly SupportedProvider Rabbit = new() { Name = "RabbitMq" };

        private readonly IBrokerService _broker = Substitute.For<IBrokerService>();
        private readonly IRenderedComponent<MudDialogProvider> _host;

        public AddConnectionDialogTests()
        {
            Services.AddSingleton(new ComponentRegistry<IConnectionDataProvider>()
                .Register<RabbitMqConnectionData>("RabbitMq")
                .Register<AmazonConnectionData>("Amazon"));

            var catalog = Substitute.For<IAwsProfileCatalog>();
            catalog.GetProfiles().Returns(new AwsProfileListing([new AwsProfile("dev", "us-east-1")], Readable: true));
            Services.AddSingleton(catalog);
            Services.AddSingleton(_broker);

            AddPopoverProvider();
            _host = Render<MudDialogProvider>();
        }

        private async Task<IDialogReference> OpenAsync()
        {
            var dialogs = Services.GetRequiredService<IDialogService>();
            var parameters = new DialogParameters { { "SupportedProviders", new List<SupportedProvider> { Amazon, Rabbit } } };

            IDialogReference reference = null!;
            await _host.InvokeAsync(async () => reference = await dialogs.ShowAsync<AddConnectionDialog>("Add New Connection", parameters));
            return reference;
        }

        private Task PickProviderAsync(SupportedProvider provider)
        {
            var select = _host.FindComponent<MudSelect<SupportedProvider>>();
            return _host.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(provider));
        }

        private Task PickAsync(string label, string value)
        {
            var select = _host.FindComponents<MudSelect<string>>().Single(s => s.Instance.Label == label);
            return _host.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(value));
        }

        private Task ClickAsync(string text)
            => _host.FindAll("button").First(b => b.TextContent.Contains(text)).ClickAsync(new MouseEventArgs());

        private bool IsOpen => _host.FindComponents<AddConnectionDialog>().Count > 0;

        [Fact(DisplayName = "A failed connection keeps the dialog open and says why")]
        public async Task A_failure_is_shown_in_the_dialog()
        {
            _broker.CreateConnectionAsync(Arg.Any<ConnectionData>())
                .Returns(new Failure<CreateConnectionResponse>("AWS profile 'dev' was not found."));
            var reference = await OpenAsync();
            await PickProviderAsync(Amazon);

            await ClickAsync("Connect");

            IsOpen.Should().BeTrue("what was typed should still be there to correct and retry");
            reference.Result.IsCompleted.Should().BeFalse();
            _host.FindComponents<MudAlert>().Should().ContainSingle(alert => alert.Instance.Severity == Severity.Error);
            _host.Markup.Should().Contain("was not found.");
        }

        [Fact(DisplayName = "A connection that succeeds on the second try closes the dialog with that result")]
        public async Task A_retry_that_succeeds_closes_the_dialog()
        {
            var success = new Success<CreateConnectionResponse>(new CreateConnectionResponse(true, "sqs://ok", []));
            _broker.CreateConnectionAsync(Arg.Any<ConnectionData>())
                .Returns(new Failure<CreateConnectionResponse>("Select a region."), success);
            var reference = await OpenAsync();
            await PickProviderAsync(Amazon);
            await ClickAsync("Connect");

            await ClickAsync("Connect");

            IsOpen.Should().BeFalse();
            (await reference.Result)!.Data.Should().BeSameAs(success);
        }

        [Fact(DisplayName = "Cancelling while a connection is in flight does not strip what it will be saved with")]
        public async Task Cancelling_mid_connect_leaves_the_data_alone()
        {
            // The service saves the connection from this same object once the broker answers.
            // Losing the mode and profile here means it works now and fails to restore later.
            ConnectionData? inFlight = null;
            var gate = new TaskCompletionSource<Result<CreateConnectionResponse>>();
            _broker.CreateConnectionAsync(Arg.Do<ConnectionData>(data => inFlight = data)).Returns(gate.Task);
            await OpenAsync();
            await PickProviderAsync(Amazon);
            await PickAsync("Authentication", AwsAuthModes.Profile);
            await PickAsync("Profile", "dev");
            var connecting = ClickAsync("Connect");

            await ClickAsync("Cancel");

            inFlight!.AuthMode.Should().Be(AwsAuthModes.Profile);
            inFlight.Profile.Should().Be("dev");

            gate.SetResult(new Failure<CreateConnectionResponse>("done"));
            await connecting;
        }
    }
}
