using Microsoft.Extensions.Logging;

namespace Iris.Brokers.Test;

/// <summary>
/// Keeps every log entry as text, message and exception together, so a test can assert on
/// what would reach a log sink, which is where a leaked credential would end up.
/// </summary>
internal sealed class CapturingLoggerFactory : ILoggerFactory
{
    private readonly List<string> _entries = [];

    public IReadOnlyList<string> Entries => _entries;

    public ILogger CreateLogger(string categoryName) => new Capture(_entries);

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public void Dispose()
    {
    }

    private sealed class Capture(List<string> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => entries.Add($"{formatter(state, exception)} {exception}");
    }
}
