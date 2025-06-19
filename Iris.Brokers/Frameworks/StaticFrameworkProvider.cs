namespace Iris.Brokers.Frameworks
{
    public class StaticFrameworkProvider : IFrameworkProvider
    {
        public StaticFrameworkProvider(IEnumerable<IFramework> frameworks)
        {
            Frameworks = frameworks;
        }

        public IEnumerable<IFramework> Frameworks { get; }

        public IFramework? GetFramework(string framework)
        {
            return Frameworks.FirstOrDefault(x => x.Name.Equals(framework, StringComparison.OrdinalIgnoreCase));
        }
    }
}

