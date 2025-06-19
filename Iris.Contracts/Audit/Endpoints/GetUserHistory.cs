using System;
using Iris.Contracts.Audit.Models;

namespace Iris.Contracts.Audit.Endpoints
{
    public static class GetUserHistory
    {
        public record GetUserHistoryRequest(int Page = 1, int PageSize = 100);

        public record GetUserHistoryResponse(List<AuditRecord> History);
    }
}

