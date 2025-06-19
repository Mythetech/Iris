using System;
using Iris.Contracts.Admin;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using static Iris.Contracts.Admin.SubmitBugReport;
using Iris.Api.Services.Admin;
using System.Net;
using System.Text;
using FluentAssertions;
using Iris.Integration.Tests.Infrastructure;
using FastEndpoints.Testing;
using MassTransit;
using System.Net.Http.Json;

namespace Iris.Integration.Tests.Admin
{
    [Collection("BugReporting")]
    public class SubmitBugReportTests : TestBase<IrisTestAppFixture>
    {
        public SubmitBugReportTests(IrisTestAppFixture fixture)
        {
            _fixture = fixture;
            _client = _fixture.Client;
        }

        private readonly HttpClient _client;
        private readonly IrisTestAppFixture _fixture;



        [Fact(DisplayName = "Low Priority Bug returns Ok")]
        public async Task Can_Accept_LowPriorityBug()
        {
            // Arrange
            var request = new SubmitBugReportRequest(new BugReport(BugSeverity.Low, "Test Bug"));

            var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync($"{Routes.Api}/bugreport", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact(DisplayName = "Medium Priority Bug returns Ok")]
        public async Task Can_Accept_MediumPriorityBug()
        {
            // Arrange
            var request = new SubmitBugReportRequest(new BugReport(BugSeverity.Medium, "Test Bug"));

            var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync($"{Routes.Api}/bugreport", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact(DisplayName = "High Priority Bug returns Ok")]
        public async Task Can_Accept_HighPriorityBug()
        {
            // Arrange
            var request = new SubmitBugReportRequest(new BugReport(BugSeverity.High, "Test Bug"));

            var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync($"{Routes.Api}/bugreport", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact(DisplayName = "Critical Priority Bug returns Ok")]
        public async Task Can_Accept_CriticalPriorityBug()
        {
            // Arrange
            var request = new SubmitBugReportRequest(new BugReport(BugSeverity.Critical, "Test Bug"));

            var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync($"{Routes.Api}/bugreport", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Theory(DisplayName = "Submit Bug Report fails when Severity is out of range")]
        [InlineData(-1)]
        [InlineData(4)]
        public async Task SubmitBugReport_Fails_When_Severity_Is_Out_Of_Range(int severity)
        {
            // Arrange
            var request = new SubmitBugReportRequest(new BugReport(new BugType(severity, "TestBug", "Test Description"), "Test Bug"));
            var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync($"{Routes.Api}/bugreport", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Theory(DisplayName = "Submit Bug Report fails when Description is empty")]
        [InlineData("")]
        [InlineData(null)]
        public async Task SubmitBugReport_Fails_When_Description_Is_Empty(string description)
        {
            // Arrange
            var request = new SubmitBugReportRequest(new BugReport(BugSeverity.Low, description));
            var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync($"{Routes.Api}/bugreport", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact(DisplayName = "Submit Bug Report fails when Description is too long")]
        public async Task SubmitBugReport_Fails_When_Description_Is_Too_Long()
        {
            // Arrange
            var request = new SubmitBugReportRequest(new BugReport(BugSeverity.Low, new string('a', 1001)));
            var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync($"{Routes.Api}/bugreport", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }
}

