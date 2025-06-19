namespace Iris.Brokers.Frameworks
{
    public interface IFrameworkProvider
    {
        public IFramework? GetFramework(string framework);
    }
}

