using System.Net;
using System.Net.Http.Json;
using FastEndpoints.Testing;
using FluentAssertions;
using Iris.Api.Services.Subscriptions.Endpoints;
using Iris.Integration.Tests.Infrastructure;

namespace Iris.Integration.Tests.Subscriptions;

public class CreateSubscriptionKeyTests : TestBase<IrisTestAppFixture>
{
    public CreateSubscriptionKeyTests(IrisTestAppFixture fixture)
    {
        _fixture = fixture;
        _client = _fixture.Client;
    }

    private readonly HttpClient _client;
    private readonly IrisTestAppFixture _fixture;
    
    [Fact(DisplayName = "Can create new subscription key")]
    public async Task Can_Create_NewSubscriptionKey()
    {
        // Act
        var response = await _client.PostAsync("admin/subscriptions/key", null, CancellationToken.None);
        
        var key =  await response.Content.ReadFromJsonAsync<CreateSubscriptionKeyResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        key.Should().NotBeNull();
        key.Key.Should().NotBeEmpty();
    }
}