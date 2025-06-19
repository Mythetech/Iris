using System.Text.Json.Serialization;

namespace Iris.Contracts.Results;

public class ApiErrorResponse
{
    /// <summary>
    /// the http status code sent to the client. default is 400.
    /// </summary>
    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }

    /// <summary>
    /// the message for the error response
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = "One or more errors occurred!";

    /// <summary>
    /// the collection of errors for the current context
    /// </summary>
    [JsonPropertyName("errors")]
    public Dictionary<string, List<string>> Errors { get; set; } = new();
}