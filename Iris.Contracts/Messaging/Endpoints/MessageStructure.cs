using System;
namespace Iris.Contracts.Messaging
{
    public static class MessageStructure
    {
        public record MessageStructureRequest(string Type);

        public record MessageStructureResponse(string Json);
    }
}

