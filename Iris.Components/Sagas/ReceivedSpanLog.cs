using Iris.Components.Sagas.Messages;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Iris.Components.Sagas;

/// <summary>
/// A bounded window onto what the OTLP receiver actually decoded, including the spans Iris made
/// nothing of. The instance state can only show what it understood, so it cannot answer "I am
/// exporting and nothing appears"; this can. In-memory, cleared on restart.
/// Fed by <see cref="Consumers.SpanBatchConsumer"/>; announces changes with <see cref="ReceivedSpansChanged"/>.
/// </summary>
public sealed class ReceivedSpanLog
{
    public const int MaxSpans = 500;

    private readonly IMessageBus _bus;
    private readonly object _gate = new();
    private readonly List<SpanIngestOutcome> _recent = [];

    public ReceivedSpanLog(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Every span seen since the last clear, including those dropped from the window below.</summary>
    public int Received { get; private set; }

    /// <summary>The most recent <see cref="MaxSpans"/> spans, newest first.</summary>
    public IReadOnlyList<SpanIngestOutcome> Recent
    {
        get { lock (_gate) return Enumerable.Reverse(_recent).ToList(); }
    }

    public async Task RecordAsync(IReadOnlyList<SpanIngestOutcome> outcomes)
    {
        if (outcomes.Count == 0)
            return;

        lock (_gate)
        {
            Received += outcomes.Count;
            _recent.AddRange(outcomes);
            if (_recent.Count > MaxSpans)
                _recent.RemoveRange(0, _recent.Count - MaxSpans);
        }

        await _bus.PublishAsync(new ReceivedSpansChanged());
    }

    public async Task ClearAsync()
    {
        lock (_gate)
        {
            _recent.Clear();
            Received = 0;
        }

        await _bus.PublishAsync(new ReceivedSpansChanged());
    }
}
