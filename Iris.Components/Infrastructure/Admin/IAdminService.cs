using System;
using Iris.Contracts.Admin;
using Iris.Contracts.Results;

namespace Iris.Components.Admin
{
    public interface IAdminService
    {
        public Task<bool> SubmitBugReportAsync(BugType bugType, string message);
    }
}

