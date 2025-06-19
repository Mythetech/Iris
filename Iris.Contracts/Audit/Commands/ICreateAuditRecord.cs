namespace Iris.Contracts.Audit.Commands
{
    public interface ICreateAuditRecord
    {
        public string Action { get; init; }

        public string Target { get; init; }

        public Dictionary<string, string> Details { get; init; }
    }
}

