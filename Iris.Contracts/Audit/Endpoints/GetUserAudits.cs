using Iris.Contracts.Audit.Models;

namespace Iris.Contracts.Audit.Endpoints
{
    public class GetUserAudits
    {
        public record GetUserAuditsRequest();
        public record GetUserAuditsResponse(List<AuditRecord> AuditTrail);
    }
}

