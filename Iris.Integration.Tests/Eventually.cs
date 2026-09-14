using System.Diagnostics;

namespace Iris.Integration.Tests;

/// <summary>
/// Polls an asynchronous probe until its result satisfies a condition.
///
/// <para>
/// This replaces the fixed <c>Task.Delay</c> calls the container tests used to sit on. A
/// sleep has to be long enough for the slowest machine that will ever run it, which makes
/// every other run pay for that machine, and it is still only a guess: when a broker is
/// slower than the guess the test fails for a reason that has nothing to do with the code
/// under test.
/// </para>
///
/// <para>
/// The budget is the test's own <c>Timeout</c>, reached through
/// <c>TestContext.Current.CancellationToken</c>. xUnit v3 cancels that token when the
/// timeout expires, so a poll that never succeeds ends at the timeout with a message
/// naming what it was waiting for, rather than at the CI job's wall clock with nothing.
/// Under xUnit v2 the token did not exist and <c>Timeout</c> was inert in a parallelised
/// collection, which is why this could not be written before the v3 upgrade.
/// </para>
/// </summary>
internal static class Eventually
{
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(100);

    /// <param name="probe">
    /// Runs once per attempt. It is free to have side effects: several of these tests drive
    /// a broker forward on each attempt, for example by re-receiving a message to push its
    /// delivery count past a redrive policy.
    /// </param>
    /// <param name="satisfied">The condition that ends the poll. The satisfying value is returned.</param>
    /// <param name="expectation">
    /// Completes the sentence "timed out waiting until ...". It is the only thing the
    /// author of a future failing build will have to go on, so make it specific.
    /// </param>
    public static async Task<T> Async<T>(
        Func<CancellationToken, Task<T>> probe,
        Func<T, bool> satisfied,
        string expectation,
        CancellationToken cancellationToken,
        TimeSpan? interval = null)
    {
        if (!cancellationToken.CanBeCanceled)
        {
            throw new InvalidOperationException(
                $"Polling until {expectation} needs a token that can be cancelled, or it can spin " +
                "for the life of the CI job. Pass TestContext.Current.CancellationToken, and give " +
                "the test a Timeout so that token is actually cancelled.");
        }

        var wait = interval ?? DefaultInterval;
        var elapsed = Stopwatch.StartNew();
        var attempts = 0;

        while (true)
        {
            attempts++;

            try
            {
                var result = await probe(cancellationToken);

                if (satisfied(result))
                    return result;

                await Task.Delay(wait, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Timed out waiting until {expectation}. Gave up after {attempts} attempts " +
                    $"over {elapsed.Elapsed.TotalSeconds:F1}s, when the test's own timeout expired.");
            }
        }
    }

    /// <summary>
    /// Awaits work that a real consumer completes, with a budget and a reason.
    ///
    /// <para>
    /// The framework round trip tests used <c>Task.WhenAny(received, Task.Delay(30s))</c>,
    /// which reports "expected the same object" when it fails and leaves the delay running
    /// after the run should have ended. This keeps the same budget, honours cancellation,
    /// and says what did not happen.
    /// </para>
    /// </summary>
    public static async Task<T> CompletesAsync<T>(
        Task<T> awaited,
        TimeSpan within,
        string expectation,
        CancellationToken cancellationToken)
    {
        try
        {
            return await awaited.WaitAsync(within, cancellationToken);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException(
                $"Timed out after {within.TotalSeconds:F0}s waiting until {expectation}.");
        }
    }

    /// <summary>
    /// The common case: poll a read until it returns something.
    /// </summary>
    public static Task<IReadOnlyList<T>> NonEmptyAsync<T>(
        Func<CancellationToken, Task<IReadOnlyList<T>>> probe,
        string expectation,
        CancellationToken cancellationToken,
        TimeSpan? interval = null)
        => Async(probe, result => result.Count > 0, expectation, cancellationToken, interval);
}
