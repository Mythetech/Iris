namespace Iris.Contracts.Messaging;

public static class SampleMessageData
{
    public record SampleMessageDataRequest(string Type);

    public record SampleMessageDataResponse(string Json);
}