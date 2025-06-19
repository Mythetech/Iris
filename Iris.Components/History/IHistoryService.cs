using System;
using Iris.Contracts.Audit.Models;

namespace Iris.Components.History
{
    public interface IHistoryService
    {
        public Task<List<AuditRecord>> GetUserHistoryAsync(int page = 1, int pageSize = 100);
    }
}

