using System;
using System.Net.Http.Json;

namespace Iris.Cloud.Client.Shared.Extensions
{
    public static class HttpClientExtensions
    {
        public static async Task<HttpResponseMessage> DeleteAsJsonAsync<T>(this HttpClient client, string requestUri, T value)
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, requestUri)
            {
                Content = JsonContent.Create(value)
            };

            return await client.SendAsync(request);
        }
    }
}

