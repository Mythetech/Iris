using FluentAssertions;
using Iris.Desktop.Infrastructure.ShellEnvironment;
using Microsoft.Extensions.Logging.Abstractions;

namespace Iris.Desktop.Test.Infrastructure;

/// <summary>
/// The reader against stand-in shells: small scripts that take the same arguments a login
/// shell is given (-l -i -c command) and misbehave in the ways real shell profiles do. No
/// real interactive shell is started and the test process's environment is not touched.
/// </summary>
public sealed class LoginShellEnvironmentReaderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "iris-shell-" + Guid.NewGuid().ToString("N"));

    private readonly LoginShellEnvironmentReader _reader = new(NullLogger<LoginShellEnvironmentReader>.Instance);

    public LoginShellEnvironmentReaderTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private string Shell(string body)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Windows apps inherit their environment; the reader is not used there.");

        var path = Path.Combine(_directory, Guid.NewGuid().ToString("N"));
        File.WriteAllText(path, "#!/bin/sh\n" + body + "\n");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }

    [Fact(DisplayName = "The environment the shell prints is returned")]
    public async Task Returns_what_the_shell_printed()
    {
        var shell = Shell("""exec /bin/sh -c "$4" """);

        var text = await _reader.ReadAsync(shell, TestContext.Current.CancellationToken);

        ShellEnvironmentParser.Parse(text).Should().ContainKey("PATH");
    }

    [Fact(DisplayName = "What a shell profile prints around the command is left out")]
    public async Task Leaves_out_banners()
    {
        var shell = Shell("""
            echo "AWS_PROFILE=from-a-banner"
            /bin/sh -c "$4"
            echo "AWS_REGION=from-a-logout-script"
            """);

        var text = await _reader.ReadAsync(shell, TestContext.Current.CancellationToken);

        text.Should().NotBeNull();
        text.Should().NotContain("from-a-banner").And.NotContain("from-a-logout-script");
    }

    [Fact(DisplayName = "A profile that leaves a background process running does not stall the read")]
    public async Task A_background_process_does_not_stall_the_read()
    {
        // The background process inherits the shell's output pipe and holds it open long
        // after the shell has printed everything and exited. Waiting for the pipe to close
        // meant a five second timeout, and no environment, on every launch.
        var shell = Shell("""
            sleep 8 &
            exec /bin/sh -c "$4"
            """);

        var text = await _reader.ReadAsync(shell, TestContext.Current.CancellationToken);

        ShellEnvironmentParser.Parse(text).Should().ContainKey("PATH");
    }

    [Fact(DisplayName = "What a shell profile writes to standard error does not stall the read")]
    public async Task Noisy_standard_error_does_not_stall_the_read()
    {
        var shell = Shell("""
            i=0
            while [ $i -lt 4000 ]; do echo "a long line of noise on standard error, repeated to fill a pipe" >&2; i=$((i+1)); done
            exec /bin/sh -c "$4"
            """);

        var text = await _reader.ReadAsync(shell, TestContext.Current.CancellationToken);

        ShellEnvironmentParser.Parse(text).Should().ContainKey("PATH");
    }

    [Fact(DisplayName = "A shell that never runs the command gives nothing")]
    public async Task A_shell_that_ignores_the_command_gives_nothing()
    {
        var shell = Shell("echo not what was asked for");

        var text = await _reader.ReadAsync(shell, TestContext.Current.CancellationToken);

        text.Should().BeNull();
    }

    [Fact(DisplayName = "A shell that is not there gives nothing")]
    public async Task A_missing_shell_gives_nothing()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Windows apps inherit their environment; the reader is not used there.");

        var text = await _reader.ReadAsync(Path.Combine(_directory, "no-such-shell"), TestContext.Current.CancellationToken);

        text.Should().BeNull();
    }
}
