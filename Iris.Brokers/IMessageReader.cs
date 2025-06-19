namespace Iris.Brokers;

public interface IMessageReader
{
    public Task ReadAsync(string messageId);
}