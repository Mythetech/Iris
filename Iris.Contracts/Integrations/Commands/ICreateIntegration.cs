namespace Iris.Contracts.Integrations
{
    public interface ICreateIntegration
    {
        public string Provider { get; set; }

        public string Address { get; set; }

        public string Data { get; set; }
    }
}

