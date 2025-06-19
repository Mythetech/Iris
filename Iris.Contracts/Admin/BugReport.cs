using System;
namespace Iris.Contracts.Admin
{
    public record BugReport(BugType BugType, string Description);

    public record LowPriorityBug(string Description) : BugReport(BugSeverity.Low, Description);

    public record MediumPriorityBug(string Description) : BugReport(BugSeverity.Medium, Description);

    public record HighPriorityBug(string Description) : BugReport(BugSeverity.High, Description);

    public record CriticalPriorityBug(string Description) : BugReport(BugSeverity.Critical, Description);

}

