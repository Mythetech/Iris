using System;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using FastEndpoints.Testing;
using FluentAssertions;
using Iris.Api.Infrastructure;
using Iris.Contracts.Admin;
using Iris.Contracts.Subscriptions;
using Iris.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using static Iris.Contracts.Admin.SubmitBugReport;
using static Iris.Contracts.Subscriptions.GetActiveSubscription;
using static Iris.Contracts.User.RegisterUser;

namespace Iris.Integration.Tests.Users
{
    [Collection("Users")]
    public class RegisterUserTests : TestBase<IrisTestAppFixture>
    {
        private readonly HttpClient _client;
        private readonly IrisTestAppFixture _fixture;

        public RegisterUserTests(IrisTestAppFixture fixture)
        {

            _fixture = fixture;
            _client = _fixture.Client;
        }

        [Fact(DisplayName = "Can register new user without subscription")]
        public async Task Can_Register_NewUser()
        {
            // Arrange
            var request = new RegisterUserRequest("testemail@test.com", "Test123!");

            var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("api/register", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact(DisplayName = "Can register new user with existing subscription and has active subscription")]
        public async Task Can_Register_NewUser_With_Existing_Subscription()
        {
            // Arrange
            var existingEmail = "existingemail@test.com";

            var db = _fixture.Services.GetRequiredService<IrisCloudDbContext>();
            await db.Subscriptions.AddAsync(new Api.Services.Subscriptions.Domain.Subscription
            {
                Email = existingEmail,
                Status = "active",
                TenantId = Guid.Empty,
                UserId = Guid.Empty
            });

            await db.SaveChangesAsync();


            var registerRequest = new RegisterUserRequest(existingEmail, "Test123!");
            var registerContent = new StringContent(System.Text.Json.JsonSerializer.Serialize(registerRequest), Encoding.UTF8, "application/json");

            // Act
            var registerResponse = await _client.PostAsync("api/register", registerContent);

            // Assert
            registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Act
            var subscriptionResponse = await _client.GetFromJsonAsync<GetActiveSubscriptionResponse>($"api/subscriptions/active");

            // Assert
            subscriptionResponse.HasActiveSubscription.Should().BeTrue();
        }
    }
}

