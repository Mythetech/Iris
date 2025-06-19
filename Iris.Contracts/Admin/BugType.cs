using System;
namespace Iris.Contracts.Admin
{
    public record BugType(int Severity, string Name, string Description);

    public static class BugSeverity
    {
        public static BugType Low = new(0, "Low", "Minor error or inconvenience");

        public static BugType Medium = new(1, "Medium", "Somewhat disruptive issue");

        public static BugType High = new(2, "High", "Breaking of core functionality and errors");

        public static BugType Critical = new(3, "Critical", "Showstopping errors, application unusable");

        public static List<BugType> Severities = [Low, Medium, High, Critical];
    }

}

