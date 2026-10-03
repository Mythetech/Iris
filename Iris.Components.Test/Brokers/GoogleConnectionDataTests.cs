using Bunit;
using FluentAssertions;
using Iris.Components.Brokers;
using Iris.Contracts.Brokers.Models;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Mythetech.Framework.Infrastructure.Files;
using NSubstitute;

namespace Iris.Components.Test.Brokers;

public class GoogleConnectionDataTests : IrisTestContext
{
    private readonly IFileOpenService _files = Substitute.For<IFileOpenService>();

    public GoogleConnectionDataTests()
    {
        Services.AddSingleton(_files);
        AddPopoverProvider();
    }

    private IRenderedComponent<GoogleConnectionData> RenderForm(ConnectionData data)
        => Render<GoogleConnectionData>(parameters => parameters.Add(p => p.Data, data));

    private static Task ChooseAsync(IRenderedComponent<GoogleConnectionData> cut, GoogleCredentialsChoice choice)
    {
        var select = cut.FindComponent<MudSelect<GoogleCredentialsChoice>>();
        return cut.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(choice));
    }

    [Fact(DisplayName = "Starts on Application Default Credentials with the extra field hidden")]
    public void Starts_on_application_default_credentials()
    {
        var data = new ConnectionData();

        var cut = RenderForm(data);

        cut.Markup.Should().Contain("Project ID");
        cut.Markup.Should().Contain("Uses the login from gcloud auth application-default login.");
        cut.Find("div.invisible").Should().NotBeNull();
        var payload = cut.Instance.Payload;
        payload.AuthMode.Should().Be("ApplicationDefault");
        payload.Uri.Should().BeEmpty();
        payload.CredentialsPath.Should().BeNull();
    }

    [Fact(DisplayName = "Typing a project ID lands in the payload")]
    public async Task Project_id_is_bound()
    {
        var data = new ConnectionData();
        var cut = RenderForm(data);

        await cut.Find("input").ChangeAsync(new() { Value = "my-project" });

        cut.Instance.Payload.ProjectId.Should().Be("my-project");
    }

    [Fact(DisplayName = "Choosing the emulator shows a host field that fills Uri")]
    public async Task Emulator_fills_uri()
    {
        var data = new ConnectionData();
        var cut = RenderForm(data);

        await ChooseAsync(cut, GoogleCredentialsChoice.Emulator);
        cut.FindAll("div.invisible").Should().BeEmpty();
        var host = cut.FindComponents<MudTextField<string>>().Single(f => f.Instance.Label == "Emulator host");
        await host.Find("input").ChangeAsync(new() { Value = "localhost:8085" });

        var payload = cut.Instance.Payload;
        payload.AuthMode.Should().Be("Emulator");
        payload.Uri.Should().Be("localhost:8085");
        payload.CredentialsPath.Should().BeNull();
    }

    [Fact(DisplayName = "Choosing a credentials file shows a path field that fills CredentialsPath")]
    public async Task Credentials_file_fills_path()
    {
        var data = new ConnectionData();
        var cut = RenderForm(data);

        await ChooseAsync(cut, GoogleCredentialsChoice.CredentialsFile);
        cut.FindAll("div.invisible").Should().BeEmpty();
        var path = cut.FindComponents<MudTextField<string>>().Single(f => f.Instance.Label == "Credentials file");
        await path.Find("input").ChangeAsync(new() { Value = "/keys/sa.json" });

        var payload = cut.Instance.Payload;
        payload.AuthMode.Should().Be("CredentialsFile");
        payload.CredentialsPath.Should().Be("/keys/sa.json");
        payload.Uri.Should().BeEmpty();
    }

    [Fact(DisplayName = "Switching source clears what the previous source filled in")]
    public async Task Switching_clears_the_other_field()
    {
        var data = new ConnectionData();
        var cut = RenderForm(data);

        await ChooseAsync(cut, GoogleCredentialsChoice.Emulator);
        data.Uri = "localhost:8085";
        await ChooseAsync(cut, GoogleCredentialsChoice.CredentialsFile);
        data.Uri.Should().BeEmpty();

        data.CredentialsPath = "/keys/sa.json";
        await ChooseAsync(cut, GoogleCredentialsChoice.ApplicationDefault);
        data.CredentialsPath.Should().BeNull();
        data.Uri.Should().BeEmpty();
    }

    [Fact(DisplayName = "Browsing puts the chosen file in CredentialsPath")]
    public async Task Browse_sets_the_path()
    {
        _files.OpenFileAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<FileFilter[]?>())
            .Returns(["/keys/picked.json"]);
        var data = new ConnectionData();
        var cut = RenderForm(data);
        await ChooseAsync(cut, GoogleCredentialsChoice.CredentialsFile);

        var path = cut.FindComponents<MudTextField<string>>().Single(f => f.Instance.Label == "Credentials file");
        await cut.InvokeAsync(() => path.Instance.OnAdornmentClick.InvokeAsync(new MouseEventArgs()));

        data.CredentialsPath.Should().Be("/keys/picked.json");
    }

    [Fact(DisplayName = "Cancelling the file dialog leaves the path alone")]
    public async Task Cancelled_browse_changes_nothing()
    {
        _files.OpenFileAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<FileFilter[]?>())
            .Returns([]);
        var data = new ConnectionData();
        var cut = RenderForm(data);
        await ChooseAsync(cut, GoogleCredentialsChoice.CredentialsFile);
        data.CredentialsPath = "/keys/typed.json";

        var path = cut.FindComponents<MudTextField<string>>().Single(f => f.Instance.Label == "Credentials file");
        await cut.InvokeAsync(() => path.Instance.OnAdornmentClick.InvokeAsync(new MouseEventArgs()));

        data.CredentialsPath.Should().Be("/keys/typed.json");
    }

    [Fact(DisplayName = "A payload that already chose the emulator opens on the emulator")]
    public void Existing_emulator_payload_opens_on_emulator()
    {
        var cut = RenderForm(new ConnectionData { AuthMode = "Emulator", Uri = "localhost:8085", ProjectId = "p" });

        cut.FindAll("div.invisible").Should().BeEmpty();
        cut.FindComponents<MudTextField<string>>().Should().Contain(f => f.Instance.Label == "Emulator host");
        cut.Instance.Payload.Uri.Should().Be("localhost:8085");
    }

    [Fact(DisplayName = "Choosing the emulator and leaving the host blank still says emulator")]
    public async Task A_blank_emulator_host_is_still_an_emulator_choice()
    {
        var cut = RenderForm(new ConnectionData { ProjectId = "my-real-project" });

        await ChooseAsync(cut, GoogleCredentialsChoice.Emulator);

        // The broker picks real credentials when nothing says otherwise, so the choice has to
        // travel in the payload rather than be guessed from which field was filled in.
        var payload = cut.Instance.Payload;
        payload.AuthMode.Should().Be("Emulator");
        payload.Uri.Should().BeEmpty();
    }

    [Fact(DisplayName = "The payload carries nothing another provider's form left behind")]
    public void Payload_holds_only_the_google_fields()
    {
        // The dialog hands every provider's form the same object, so switching from another
        // provider arrives with that provider's values still in it.
        var cut = RenderForm(new ConnectionData
        {
            Uri = "amqp://guest@localhost",
            Username = "AKIAEXAMPLE",
            Password = "secret",
            ConnectionString = "Endpoint=sb://example;",
            Region = "eu-west-1",
            VHost = "/orders",
            ProjectId = "my-project",
        });

        cut.Find("div.invisible").Should().NotBeNull();
        var payload = cut.Instance.Payload;
        payload.ProjectId.Should().Be("my-project");
        payload.AuthMode.Should().Be("ApplicationDefault");
        payload.Uri.Should().BeEmpty();
        payload.Username.Should().BeNull();
        payload.Password.Should().BeNull();
        payload.ConnectionString.Should().BeNull();
        payload.Region.Should().BeNull();
        payload.VHost.Should().BeNull();
    }
}
