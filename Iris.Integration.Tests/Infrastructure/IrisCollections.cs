using System;
using FastEndpoints.Testing;

namespace Iris.Integration.Tests.Infrastructure
{
    public class IrisCollections
    {
        public static string Default = "Iris";

        public const string _Default = "Iris";

        [CollectionDefinition(_Default, DisableParallelization = true)]
        public class IrisDefaultApiCollection : TestCollection<IrisTestAppFixture>
        {
            public const string Name = "Iris";
        }

        [CollectionDefinition("BugReporting", DisableParallelization = true)]
        public class BugReportingCollection : TestCollection<IrisTestAppFixture>
        {
            public const string Name = nameof(BugReportingCollection);
        }

        [CollectionDefinition("User", DisableParallelization = true)]
        public class UserCollection : TestCollection<IrisTestAppFixture>
        {
            public const string Name = nameof(UserCollection);
        }

        [CollectionDefinition("IsolatedProvider", DisableParallelization = true)]
        public class IsolatedProviderCollection : TestCollection<IrisTestAppFixture>
        {
            public const string Name = "IsolatedProvider";
        }
    }
}

