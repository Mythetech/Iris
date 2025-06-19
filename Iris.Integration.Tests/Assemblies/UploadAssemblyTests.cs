using System.Net.Http.Headers;
using FastEndpoints.Testing;
using FluentAssertions;
using Iris.Integration.Tests.Infrastructure;
using UploadAssemblyRequest = Iris.Api.Services.Assemblies.Endpoints.UploadAssemblyRequest;

namespace Iris.Integration.Tests.Assemblies;

public class UploadAssemblyTests : TestBase<IrisTestAppFixture>
{
    private const string TestAssemblyFolder = "Assemblies/TestAssemblyModules";

    private readonly IrisTestAppFixture _fixture;
    private readonly HttpClient _client;

    public UploadAssemblyTests(IrisTestAppFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Theory(DisplayName = "Can upload an assembly successfully")]
    [InlineData("Iris.Cloud.Demo.Contracts.dll")]
    public async Task Can_Upload_Assembly(string assemblyName)
    {
        // Arrange
        using var stream = File.OpenRead(Path.Combine(TestAssemblyFolder, assemblyName));
        stream.Position = 0;
        using var content = new MultipartFormDataContent();

        var fileContent = new StreamContent(stream);

        fileContent.Headers.ContentType = new MediaTypeHeaderValue("multipart/form-data");

        content.Add(
            content: fileContent,
            name: "\"file\"",
            fileName: assemblyName);

        var resp = await _client.PostAsync("/api/assemblies/upload", content);
        
        resp.IsSuccessStatusCode.Should().BeTrue();
    }
}