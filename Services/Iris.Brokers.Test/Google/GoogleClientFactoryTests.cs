using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using Iris.Brokers.Exceptions;
using Iris.Brokers.Google;
using Iris.Brokers.Models;
using Xunit;

namespace Iris.Brokers.Test.Google;

public class GoogleClientFactoryTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("iris-google-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    private string WriteJson(string name, object content) => WriteFile(name, JsonSerializer.Serialize(content));

    /// <summary>
    /// A service account file the Google library will accept: the key is a real RSA key,
    /// generated here, so loading it needs no network and no account.
    /// </summary>
    private string WriteServiceAccount(string name = "sa.json", string? projectId = "from-file")
    {
        using var rsa = RSA.Create(2048);

        return WriteJson(name, new Dictionary<string, string?>
        {
            ["type"] = "service_account",
            ["project_id"] = projectId,
            ["private_key_id"] = "abc",
            ["private_key"] = rsa.ExportPkcs8PrivateKeyPem(),
            ["client_email"] = "iris@from-file.iam.gserviceaccount.com",
            ["client_id"] = "1",
            ["token_uri"] = "https://oauth2.googleapis.com/token",
        });
    }

    // No Application Default Credentials file unless a test says where one is: what happens
    // to be on the machine running the tests must not decide their outcome.
    private static Task<GoogleConnectionSettings> Resolve(ConnectionData data, string? applicationDefaultFile = null)
        => GoogleClientFactory.ResolveAsync(data, () => applicationDefaultFile, TestContext.Current.CancellationToken);

    private static Func<string, string?> Variables(params (string Name, string Value)[] variables)
        => name => variables.FirstOrDefault(v => v.Name == name).Value;

    [Fact]
    public async Task With_nothing_but_a_project_the_source_is_application_default_credentials()
    {
        var settings = await Resolve(new ConnectionData { ProjectId = "my-project" });

        settings.Source.Should().Be(GoogleCredentialSource.ApplicationDefault);
        settings.ProjectId.Should().Be("my-project");
        settings.Address.Should().Be("pubsub.googleapis.com/projects/my-project");
        settings.EmulatorHost.Should().BeNull();
        settings.CredentialsPath.Should().BeNull();
    }

    [Fact]
    public async Task A_uri_selects_the_emulator_and_its_host_is_part_of_the_address()
    {
        var settings = await Resolve(new ConnectionData { Uri = "localhost:8085", ProjectId = "my-project" });

        settings.Source.Should().Be(GoogleCredentialSource.Emulator);
        settings.EmulatorHost.Should().Be("localhost:8085");
        // The manager refuses a second connection with the same address, so an emulator and a
        // real project of the same name must not produce the same one.
        settings.Address.Should().Be("localhost:8085/projects/my-project");
    }

    [Fact]
    public async Task The_emulator_wins_when_a_stale_credentials_path_is_also_present()
    {
        var settings = await Resolve(new ConnectionData
        {
            Uri = "localhost:8085",
            CredentialsPath = "/no/such/file.json",
            ProjectId = "my-project",
        });

        settings.Source.Should().Be(GoogleCredentialSource.Emulator);
    }

    [Theory]
    [InlineData(" localhost:8085 ")]
    [InlineData("http://localhost:8085")]
    [InlineData("HTTP://localhost:8085/")]
    public async Task An_emulator_host_pasted_as_a_url_is_reduced_to_host_and_port(string typed)
    {
        var settings = await Resolve(new ConnectionData { Uri = typed, ProjectId = "my-project" });

        settings.EmulatorHost.Should().Be("localhost:8085");
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("not a host")]
    [InlineData("localhost:8085/v1")]
    [InlineData("https://localhost:8085")]
    [InlineData("localhost:0")]
    [InlineData("localhost:99999")]
    [InlineData("localhost:8085:8085")]
    [InlineData("::1:8085")]
    public async Task An_emulator_host_without_a_usable_host_and_port_is_refused(string typed)
    {
        var act = () => Resolve(new ConnectionData { Uri = typed, ProjectId = "my-project" });

        (await act.Should().ThrowAsync<InvalidConnectionException>())
            .WithMessage(PubSubErrors.EmulatorHostInvalid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_project_id_is_required_when_nothing_else_names_one(string? projectId)
    {
        var adc = () => Resolve(new ConnectionData { ProjectId = projectId });
        var emulator = () => Resolve(new ConnectionData { Uri = "localhost:8085", ProjectId = projectId });

        (await adc.Should().ThrowAsync<InvalidConnectionException>()).WithMessage(PubSubErrors.ProjectIdRequired);
        (await emulator.Should().ThrowAsync<InvalidConnectionException>()).WithMessage(PubSubErrors.ProjectIdRequired);
    }

    [Fact]
    public async Task A_credentials_file_supplies_the_project_when_the_form_leaves_it_blank()
    {
        var path = WriteServiceAccount();

        var settings = await Resolve(new ConnectionData { CredentialsPath = path });

        settings.Source.Should().Be(GoogleCredentialSource.CredentialsFile);
        settings.ProjectId.Should().Be("from-file");
        settings.CredentialsPath.Should().Be(path);
        settings.CredentialType.Should().Be("service_account");
        settings.Address.Should().Be("pubsub.googleapis.com/projects/from-file");
    }

    [Fact]
    public async Task The_form_project_beats_the_one_in_the_credentials_file()
    {
        var settings = await Resolve(new ConnectionData { CredentialsPath = WriteServiceAccount(), ProjectId = " from-form " });

        settings.ProjectId.Should().Be("from-form");
    }

    [Fact]
    public async Task A_credentials_file_with_no_project_still_needs_one_from_the_form()
    {
        // What gcloud writes for a user login: it has no project_id at all.
        var path = WriteJson("user.json", new { type = "authorized_user", client_id = "id", client_secret = "secret", refresh_token = "token" });

        var act = () => Resolve(new ConnectionData { CredentialsPath = path });

        (await act.Should().ThrowAsync<InvalidConnectionException>()).WithMessage(PubSubErrors.ProjectIdRequired);
    }

    [Theory]
    [InlineData("service_account")]
    [InlineData("authorized_user")]
    [InlineData("impersonated_service_account")]
    [InlineData("external_account")]
    [InlineData("external_account_authorized_user")]
    public async Task Every_credential_type_application_default_credentials_accepts_is_accepted(string type)
    {
        var path = WriteJson("typed.json", new { type });

        var settings = await Resolve(new ConnectionData { CredentialsPath = path, ProjectId = "my-project" });

        settings.CredentialType.Should().Be(type);
    }

    [Fact]
    public async Task A_missing_credentials_file_says_so()
    {
        var act = () => Resolve(new ConnectionData { CredentialsPath = Path.Combine(_directory, "nope.json"), ProjectId = "p" });

        (await act.Should().ThrowAsync<InvalidConnectionException>()).WithMessage(PubSubErrors.CredentialsFileMissing);
    }

    [Fact]
    public async Task A_directory_is_not_a_credentials_file()
    {
        var act = () => Resolve(new ConnectionData { CredentialsPath = _directory, ProjectId = "p" });

        (await act.Should().ThrowAsync<InvalidConnectionException>()).WithMessage(PubSubErrors.CredentialsFileMissing);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("")]
    [InlineData("[1, 2]")]
    [InlineData("{}")]
    [InlineData("{\"type\": 7}")]
    [InlineData("{\"type\": \"something_else\"}")]
    public async Task A_file_that_is_not_google_credentials_is_refused_without_quoting_the_parser(string content)
    {
        var path = WriteFile("other.json", content);

        var act = () => Resolve(new ConnectionData { CredentialsPath = path, ProjectId = "p" });

        (await act.Should().ThrowAsync<InvalidConnectionException>()).WithMessage(PubSubErrors.NotACredentialsFile);
    }

    [Fact]
    public async Task Emulator_clients_are_built_without_touching_the_network()
    {
        var settings = await Resolve(new ConnectionData { Uri = "localhost:1", ProjectId = "my-project" });

        var (publisher, subscriber) = await GoogleClientFactory.CreateClientsAsync(settings, TestContext.Current.CancellationToken);

        publisher.Should().NotBeNull();
        subscriber.Should().NotBeNull();
    }

    [Fact]
    public async Task A_service_account_file_builds_clients_without_touching_the_network()
    {
        var settings = await Resolve(new ConnectionData { CredentialsPath = WriteServiceAccount() });

        var (publisher, subscriber) = await GoogleClientFactory.CreateClientsAsync(settings, TestContext.Current.CancellationToken);

        publisher.Should().NotBeNull();
        subscriber.Should().NotBeNull();
    }

    [Fact]
    public async Task A_credentials_file_with_a_broken_key_is_refused_with_the_same_wording()
    {
        var path = WriteJson("broken.json", new
        {
            type = "service_account",
            project_id = "from-file",
            private_key = "not a key",
            client_email = "iris@from-file.iam.gserviceaccount.com",
        });
        var settings = await Resolve(new ConnectionData { CredentialsPath = path });

        var act = () => GoogleClientFactory.CreateClientsAsync(settings, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<InvalidConnectionException>()).WithMessage(PubSubErrors.NotACredentialsFile);
    }

    [Theory]
    [InlineData("127.0.0.1:8085")]
    [InlineData("[::1]:8085")]
    [InlineData("pubsub-emulator.internal:65535")]
    public async Task An_emulator_host_in_any_usable_form_is_kept_as_typed(string typed)
    {
        var settings = await Resolve(new ConnectionData { Uri = typed, ProjectId = "my-project" });

        settings.EmulatorHost.Should().Be(typed);
    }

    [Fact]
    public async Task Choosing_the_emulator_and_leaving_the_host_blank_is_refused_rather_than_reaching_real_pubsub()
    {
        var act = () => Resolve(new ConnectionData { CredentialSource = "Emulator", Uri = "  ", ProjectId = "my-real-project" });

        (await act.Should().ThrowAsync<InvalidConnectionException>()).WithMessage(PubSubErrors.EmulatorHostInvalid);
    }

    [Fact]
    public async Task Choosing_a_credentials_file_and_leaving_the_path_blank_is_refused_rather_than_using_the_machine_login()
    {
        var act = () => Resolve(new ConnectionData { CredentialSource = "CredentialsFile", ProjectId = "my-project" });

        (await act.Should().ThrowAsync<InvalidConnectionException>()).WithMessage(PubSubErrors.CredentialsFileMissing);
    }

    [Fact]
    public async Task Choosing_application_default_credentials_ignores_what_other_choices_left_behind()
    {
        var settings = await Resolve(new ConnectionData
        {
            CredentialSource = "ApplicationDefault",
            Uri = "localhost:8085",
            CredentialsPath = "/no/such/file.json",
            ProjectId = "my-project",
        });

        settings.Source.Should().Be(GoogleCredentialSource.ApplicationDefault);
        settings.Address.Should().Be("pubsub.googleapis.com/projects/my-project");
        settings.EmulatorHost.Should().BeNull();
    }

    [Theory]
    [InlineData("SignInWithGoogle")]
    [InlineData("2")]
    public async Task A_credential_source_this_version_does_not_know_is_refused(string source)
    {
        var act = () => Resolve(new ConnectionData { CredentialSource = source, Uri = "localhost:8085", ProjectId = "my-project" });

        (await act.Should().ThrowAsync<InvalidConnectionException>()).WithMessage(PubSubErrors.CredentialSourceUnknown);
    }

    [Fact]
    public async Task Application_default_credentials_found_on_the_machine_are_loaded_from_their_file()
    {
        var file = WriteServiceAccount("adc.json");

        var settings = await Resolve(new ConnectionData { ProjectId = "my-project" }, applicationDefaultFile: file);

        settings.Source.Should().Be(GoogleCredentialSource.ApplicationDefault);
        settings.CredentialsPath.Should().Be(file);
        settings.CredentialType.Should().Be("service_account");
        settings.ProjectId.Should().Be("my-project");
    }

    [Fact]
    public async Task A_login_made_after_a_failed_connect_works_without_restarting()
    {
        // What the Google library alone cannot do: it caches its lookup, a failure included,
        // for the life of the process.
        var file = WriteFile("adc.json", "{\"type\": \"authorized_user\"}");
        var broken = await Resolve(new ConnectionData { ProjectId = "my-project" }, applicationDefaultFile: file);
        var first = () => GoogleClientFactory.CreateClientsAsync(broken, TestContext.Current.CancellationToken);
        (await first.Should().ThrowAsync<InvalidConnectionException>())
            .WithMessage(PubSubErrors.ApplicationDefaultCredentialsUnreadable);

        WriteJson("adc.json", new { type = "authorized_user", client_id = "id", client_secret = "secret", refresh_token = "token" });
        var settings = await Resolve(new ConnectionData { ProjectId = "my-project" }, applicationDefaultFile: file);
        var (publisher, subscriber) = await GoogleClientFactory.CreateClientsAsync(settings, TestContext.Current.CancellationToken);

        publisher.Should().NotBeNull();
        subscriber.Should().NotBeNull();
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("{\"type\": \"something_else\"}")]
    public async Task Application_default_credentials_that_cannot_be_read_say_so(string content)
    {
        var file = WriteFile("adc.json", content);

        var act = () => Resolve(new ConnectionData { ProjectId = "my-project" }, applicationDefaultFile: file);

        (await act.Should().ThrowAsync<InvalidConnectionException>())
            .WithMessage(PubSubErrors.ApplicationDefaultCredentialsUnreadable);
    }

    [Fact]
    public async Task Application_default_credentials_pointing_at_a_file_that_is_gone_say_so()
    {
        var act = () => Resolve(
            new ConnectionData { ProjectId = "my-project" },
            applicationDefaultFile: Path.Combine(_directory, "gone.json"));

        (await act.Should().ThrowAsync<InvalidConnectionException>())
            .WithMessage(PubSubErrors.ApplicationDefaultCredentialsUnreadable);
    }

    [Fact]
    public void An_explicitly_named_application_default_file_wins_over_the_gcloud_login()
    {
        var gcloud = Directory.CreateDirectory(Path.Combine(_directory, ".config", "gcloud")).FullName;
        File.WriteAllText(Path.Combine(gcloud, "application_default_credentials.json"), "{}");

        var found = GoogleClientFactory.FindApplicationDefaultCredentialsFile(
            Variables(("GOOGLE_APPLICATION_CREDENTIALS", "/keys/explicit.json"), ("HOME", _directory)));

        found.Should().Be("/keys/explicit.json");
    }

    [Fact]
    public void The_gcloud_login_is_found_under_the_home_directory()
    {
        var gcloud = Directory.CreateDirectory(Path.Combine(_directory, ".config", "gcloud")).FullName;
        var file = Path.Combine(gcloud, "application_default_credentials.json");
        File.WriteAllText(file, "{}");

        GoogleClientFactory.FindApplicationDefaultCredentialsFile(Variables(("HOME", _directory))).Should().Be(file);
    }

    [Fact]
    public void On_windows_the_gcloud_login_is_found_under_appdata()
    {
        var gcloud = Directory.CreateDirectory(Path.Combine(_directory, "gcloud")).FullName;
        var file = Path.Combine(gcloud, "application_default_credentials.json");
        File.WriteAllText(file, "{}");

        GoogleClientFactory.FindApplicationDefaultCredentialsFile(
            Variables(("APPDATA", _directory), ("HOME", "/somewhere/else"))).Should().Be(file);
    }

    [Fact]
    public void A_machine_with_no_gcloud_login_has_no_file_to_read()
    {
        GoogleClientFactory.FindApplicationDefaultCredentialsFile(Variables(("HOME", _directory))).Should().BeNull();
        GoogleClientFactory.FindApplicationDefaultCredentialsFile(Variables()).Should().BeNull();
    }

    [Fact]
    public async Task A_user_login_with_no_quota_project_is_billed_to_the_connections_project()
    {
        var path = WriteJson("user.json", new { type = "authorized_user", client_id = "id", client_secret = "secret", refresh_token = "token" });
        var settings = await Resolve(new ConnectionData { CredentialsPath = path, ProjectId = "my-project" });

        var credential = await GoogleClientFactory.LoadCredentialAsync(settings, TestContext.Current.CancellationToken);

        credential.QuotaProject.Should().Be("my-project");
    }

    [Fact]
    public async Task A_user_login_that_names_its_own_quota_project_keeps_it()
    {
        var path = WriteJson("user.json", new
        {
            type = "authorized_user",
            client_id = "id",
            client_secret = "secret",
            refresh_token = "token",
            quota_project_id = "billing-project",
        });
        var settings = await Resolve(new ConnectionData { CredentialsPath = path, ProjectId = "my-project" });

        var credential = await GoogleClientFactory.LoadCredentialAsync(settings, TestContext.Current.CancellationToken);

        credential.QuotaProject.Should().Be("billing-project");
    }

    [Fact]
    public async Task A_service_account_is_not_given_a_quota_project()
    {
        var settings = await Resolve(new ConnectionData { CredentialsPath = WriteServiceAccount(), ProjectId = "my-project" });

        var credential = await GoogleClientFactory.LoadCredentialAsync(settings, TestContext.Current.CancellationToken);

        credential.QuotaProject.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task Settings_never_hold_key_material()
    {
        var settings = await Resolve(new ConnectionData { CredentialsPath = WriteServiceAccount() });

        settings.ToString().Should().NotContain("PRIVATE KEY");
    }
}
