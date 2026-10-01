using FluentAssertions;
using Iris.Desktop.Infrastructure.ShellEnvironment;

namespace Iris.Desktop.Test.Infrastructure;

/// <summary>
/// The parts of the shell environment import that decide what gets copied. They work on
/// dictionaries, so nothing here starts a shell or changes the test process's environment.
/// </summary>
public class ShellEnvironmentTests
{
    private static Dictionary<string, string> Vars(params (string Name, string Value)[] pairs)
        => pairs.ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);

    [Fact(DisplayName = "Well-formed lines become variables")]
    public void Parses_name_value_lines()
    {
        var parsed = ShellEnvironmentParser.Parse("AWS_PROFILE=dev\nPATH=/usr/bin:/bin\n");

        parsed.Should().Equal(Vars(("AWS_PROFILE", "dev"), ("PATH", "/usr/bin:/bin")));
    }

    [Fact(DisplayName = "A value keeps every '=' after the first")]
    public void Keeps_equals_signs_in_values()
    {
        ShellEnvironmentParser.Parse("AWS_CA_BUNDLE=/etc/ssl/a=b.pem")["AWS_CA_BUNDLE"].Should().Be("/etc/ssl/a=b.pem");
    }

    [Fact(DisplayName = "Banners, blank lines and malformed lines are ignored")]
    public void Ignores_noise()
    {
        var parsed = ShellEnvironmentParser.Parse("Welcome back, Tom!\n\n=novalue\n1BAD=x\nhas space=x\nAWS_REGION=eu-west-1\n");

        parsed.Should().Equal(Vars(("AWS_REGION", "eu-west-1")));
    }

    [Fact(DisplayName = "Windows line endings do not end up in values")]
    public void Strips_carriage_returns()
    {
        ShellEnvironmentParser.Parse("AWS_PROFILE=dev\r\n")["AWS_PROFILE"].Should().Be("dev");
    }

    [Theory(DisplayName = "Nothing to parse gives nothing")]
    [InlineData(null)]
    [InlineData("")]
    public void Empty_input_is_empty(string? text)
    {
        ShellEnvironmentParser.Parse(text).Should().BeEmpty();
    }

    [Fact(DisplayName = "AWS variables the process lacks are copied")]
    public void Imports_missing_aws_variables()
    {
        var selected = ShellEnvironmentImport.Select(
            current: Vars(("HOME", "/Users/tom")),
            shell: Vars(("AWS_PROFILE", "dev"), ("AWS_REGION", "eu-west-1")));

        selected.Should().Equal(Vars(("AWS_PROFILE", "dev"), ("AWS_REGION", "eu-west-1")));
    }

    [Fact(DisplayName = "A variable the process was started with is never overwritten")]
    public void The_process_wins()
    {
        var selected = ShellEnvironmentImport.Select(
            current: Vars(("AWS_PROFILE", "from-terminal")),
            shell: Vars(("AWS_PROFILE", "from-zshrc")));

        selected.Should().BeEmpty();
    }

    [Fact(DisplayName = "An empty value in the process counts as not set")]
    public void An_empty_process_value_is_replaced()
    {
        var selected = ShellEnvironmentImport.Select(
            current: Vars(("AWS_PROFILE", "")),
            shell: Vars(("AWS_PROFILE", "dev")));

        selected.Should().Equal(Vars(("AWS_PROFILE", "dev")));
    }

    [Fact(DisplayName = "Nothing outside AWS_ and PATH is copied")]
    public void Other_variables_stay_out()
    {
        var selected = ShellEnvironmentImport.Select(
            current: Vars(),
            shell: Vars(("GITHUB_TOKEN", "secret"), ("DOTNET_ROOT", "/opt/dotnet"), ("AWSOME", "no"), ("aws_profile", "lower")));

        selected.Should().BeEmpty();
    }

    [Fact(DisplayName = "An empty AWS value in the shell is not copied")]
    public void Empty_shell_values_stay_out()
    {
        ShellEnvironmentImport.Select(Vars(), Vars(("AWS_PROFILE", ""))).Should().BeEmpty();
    }

    [Fact(DisplayName = "PATH keeps the process's entries first and appends only new ones, in order")]
    public void Path_is_merged()
    {
        var selected = ShellEnvironmentImport.Select(
            current: Vars(("PATH", "/usr/bin:/bin")),
            shell: Vars(("PATH", "/opt/homebrew/bin:/usr/bin::/Users/tom/.local/bin:/bin")));

        selected.Should().Equal(Vars(("PATH", "/usr/bin:/bin:/opt/homebrew/bin:/Users/tom/.local/bin")));
    }

    [Fact(DisplayName = "A shell PATH that adds nothing leaves PATH alone")]
    public void An_unchanged_path_is_not_set()
    {
        var selected = ShellEnvironmentImport.Select(
            current: Vars(("PATH", "/usr/bin:/bin")),
            shell: Vars(("PATH", "/bin:/usr/bin")));

        selected.Should().BeEmpty();
    }

    [Fact(DisplayName = "A process with no PATH takes the shell's")]
    public void A_missing_path_takes_the_shells()
    {
        var selected = ShellEnvironmentImport.Select(Vars(), Vars(("PATH", "/usr/bin:/bin")));

        selected.Should().Equal(Vars(("PATH", "/usr/bin:/bin")));
    }

    [Theory(DisplayName = "EC2 metadata lookups are switched off unless something already decided")]
    [InlineData(null, "true")]
    [InlineData("", "true")]
    [InlineData("false", null)]
    [InlineData("true", null)]
    public void Ec2_metadata_default(string? existing, string? expected)
    {
        var current = existing is null ? Vars() : Vars((AwsEnvironmentDefaults.Ec2MetadataDisabled, existing));

        var selected = AwsEnvironmentDefaults.Select(current);

        if (expected is null)
            selected.Should().BeEmpty();
        else
            selected.Should().Equal(Vars((AwsEnvironmentDefaults.Ec2MetadataDisabled, expected)));
    }
}
