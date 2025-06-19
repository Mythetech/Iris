using System;
using Iris.Contracts.Messaging.Models;

namespace Iris.Contracts.Messaging
{
    public static class SendMessage
    {
        public record SendMessageRequest(Message Message);

        public record SendMessageResponse(bool Success);
    }
}

