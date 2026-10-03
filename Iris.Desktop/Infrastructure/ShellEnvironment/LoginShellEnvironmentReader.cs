using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Iris.Desktop.Infrastructure.ShellEnvironment;

/// <summary>
/// Asks the user's login shell what its environment is. An app started from the Dock, Finder
/// or a desktop launcher gets a minimal environment, not the one a shell profile builds, so
/// variables such as AWS_PROFILE and the Homebrew part of PATH are missing from it.
/// </summary>
public class LoginShellEnvironmentReader
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly ILogger<LoginShellEnvironmentReader> _logger;

    public LoginShellEnvironmentReader(ILogger<LoginShellEnvironmentReader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// The text <c>env</c> printed, or null when the shell could not be asked.
    /// </summary>
    public Task<string?> ReadAsync(CancellationToken cancellationToken = default)
    {
        var shell = Environment.GetEnvironmentVariable("SHELL");

        if (string.IsNullOrWhiteSpace(shell))
            shell = OperatingSystem.IsMacOS() ? "/bin/zsh" : "/bin/bash";

        return ReadAsync(shell, cancellationToken);
    }

    public async Task<string?> ReadAsync(string shell, CancellationToken cancellationToken = default)
    {
        // A shell profile can print a banner before and after the command runs. The marker is
        // new each time so nothing a profile prints can be mistaken for it.
        var marker = Guid.NewGuid().ToString("N");

        // Started through sh so the shell's standard error goes to /dev/null. A pipe for it
        // would have to be drained for as long as anything held it open, and what a profile
        // writes there is of no use here.
        var startInfo = new ProcessStartInfo("/bin/sh")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("exec 2>/dev/null; exec \"$0\" -l -i -c \"$1\"");
        startInfo.ArgumentList.Add(shell);
        startInfo.ArgumentList.Add($"echo {marker}; /usr/bin/env; echo {marker}");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        Process? process = null;

        try
        {
            process = Process.Start(startInfo);

            if (process is null)
                return null;

            process.StandardInput.Close();

            var environment = await ReadBetweenMarkersAsync(process.StandardOutput, marker, timeout.Token);

            if (environment is null)
            {
                _logger.LogWarning("The login shell {Shell} did not return its environment", shell);
                return null;
            }

            await process.WaitForExitAsync(timeout.Token);

            return environment;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Reading the login shell environment from {Shell} timed out after {Seconds} seconds", shell, Timeout.TotalSeconds);
            return null;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            _logger.LogWarning("Could not read the login shell environment from {Shell}: {Reason}", shell, ex.GetType().Name);
            return null;
        }
        finally
        {
            if (process is { HasExited: false })
                process.Kill(entireProcessTree: true);

            process?.Dispose();
        }
    }

    // Stops at the second marker and does not wait for the pipe to close. A background
    // process started by the shell profile inherits the pipe and can hold it open long after
    // the shell has printed everything and gone.
    private static async Task<string?> ReadBetweenMarkersAsync(StreamReader output, string marker, CancellationToken cancellationToken)
    {
        var text = new StringBuilder();
        var buffer = new char[4096];

        while (true)
        {
            var read = await output.ReadAsync(buffer.AsMemory(), cancellationToken);

            if (read == 0)
                return null;

            text.Append(buffer, 0, read);

            var soFar = text.ToString();
            var start = soFar.IndexOf(marker, StringComparison.Ordinal);

            if (start < 0)
                continue;

            var end = soFar.IndexOf(marker, start + marker.Length, StringComparison.Ordinal);

            if (end >= 0)
                return soFar[(start + marker.Length)..end];
        }
    }
}
