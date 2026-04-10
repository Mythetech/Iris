namespace Iris.Brokers.Models;

/// <summary>
/// Which queue a reader should pull from.
/// </summary>
public enum ReadSource
{
    /// <summary>The main queue/subscription.</summary>
    Main = 0,

    /// <summary>The provider's dead-letter sub-queue. Not supported by every broker.</summary>
    DeadLetter = 1,
}
