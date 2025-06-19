using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FastEndpoints.Testing;
using FluentAssertions;
using Iris.Api.Services.Subscriptions.Endpoints;
using Iris.Contracts.Messaging;
using Iris.Cloud.Demo.Contracts;
using Iris.Integration.Tests.Infrastructure;
namespace Iris.Integration.Tests.Messaging;

public class SampleMessageDataTests : TestBase<IrisTestAppFixture>
{
    public SampleMessageDataTests(IrisTestAppFixture fixture)
    {
        _fixture = fixture;
        _client = _fixture.Client;
    }

    private readonly HttpClient _client;
    private readonly IrisTestAppFixture _fixture;

    [Fact(DisplayName = "Can create simple sample message data")]
    public async Task Can_Create_SimpleSampleMessageData()
    {
        // Act
        var response = await _client.PostAsJsonAsync("api/messaging/sample-data",
            new SampleMessageData.SampleMessageDataRequest("ChangeColorsCommand"), CancellationToken.None);

        var data = await response.Content.ReadFromJsonAsync<SampleMessageData.SampleMessageDataResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        data.Should().NotBeNull();
        data.Json.Should().NotBeEmpty();

        var colorCommand = JsonSerializer.Deserialize<ChangeColorsCommand>(data.Json);

        colorCommand.Blue.Should().BeInRange(0, 255);
        colorCommand.Red.Should().BeInRange(0, 255);
        colorCommand.Green.Should().BeInRange(0, 255);
        (colorCommand.Red + colorCommand.Green + colorCommand.Blue).Should().BeGreaterThan(0);
    }
}