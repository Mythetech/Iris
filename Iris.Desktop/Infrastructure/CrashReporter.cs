using Microsoft.Extensions.Logging;

namespace Iris.Desktop.Infrastructure;

/// <summary>
/// A windowed app has no console attached, so writing a crash to stderr leaves no trace a
/// user can retrieve. Crashes are appended to a file alongside the database, and mirrored
/// through the logging pipeline once one exists. Installed before the host is built so that
/// failures during startup are captured too.
/// </summary>
public static class CrashReporter
{
    private static ILogger? _logger;
    private static int _installed;

    public static void Install()
    {
        if (Interlocked.Exchange(ref _installed, 1) == 1)
            return;

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Report("Unhandled exception", e.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Report("Unobserved task exception", e.Exception);

            // Already recorded, so stop it propagating further into the runtime.
            e.SetObserved();
        };
    }

    /// <summary>
    /// Routes subsequent crash reports through the application's logging pipeline in
    /// addition to the crash file. Called once the host is built.
    /// </summary>
    public static void AttachLogger(ILogger logger) => _logger = logger;

    public static void Report(string context, Exception? exception)
    {
        _logger?.LogCritical(exception, "{Context}", context);

        var entry = $"[{DateTimeOffset.UtcNow:O}] {context}: {exception?.ToString() ?? "(no exception)"}";
        Console.Error.WriteLine(entry);

        try
        {
            File.AppendAllText(GetCrashLogPath(), entry + Environment.NewLine + Environment.NewLine);
        }
        catch (Exception writeException) when (writeException is IOException or UnauthorizedAccessException)
        {
            // The crash file is best effort. Failing to write it must not replace the
            // original exception with a less useful one.
        }
    }

    public static string GetCrashLogPath()
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(basePath, "Iris", "logs");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, $"crash-{DateTime.UtcNow:yyyyMMdd}.log");
    }
}
