using System;
namespace Iris.Contracts.Admin
{
    public static class SubmitBugReport
    {
        public record SubmitBugReportRequest(BugReport Bug);
    }
}

