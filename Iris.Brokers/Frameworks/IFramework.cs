namespace Iris.Brokers.Frameworks
{
    public interface IFramework
    {
        public string Name { get; }

        public string CreateWrappedMessage(IMessageRequest request);
    }
}

