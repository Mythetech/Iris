using System;
namespace Iris.Contracts.Audit
{
    /// <summary>
    /// Constants here get a lookup dictionary generated to turn the string key into a display name
    /// i.e. Messaging.Sent -> Returns `Message Sent` from the generated lookup dictionary
    /// </summary>
    public partial class Actions
    {
        public const string MessageSent = "Messaging.Sent";

        public const string ConnectionCreated = "Brokers.Connection.Created";

        public const string ConnectionRemoved = "Brokers.Connection.Deleted";

        public const string IntegrationCreated = "Integrations.Created";

        public const string IntegrationRemoved = "Integrations.Deleted";

        /* 
         //Source generator creates a public field with this signature
            public static readonly IReadOnlyDictionary<string, string> DisplayLookup       
        */
    }
}

