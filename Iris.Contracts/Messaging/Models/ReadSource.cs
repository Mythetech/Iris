namespace Iris.Contracts.Messaging.Models;

/// <summary>
/// Contract-layer mirror of <c>Iris.Brokers.Models.ReadSource</c>. Used on
/// the service boundary so that <c>Iris.Components</c> / UI projects do
/// not need a direct reference to <c>Iris.Brokers</c>.
/// </summary>
public enum ReadSource
{
    Main = 0,
    DeadLetter = 1,
}
