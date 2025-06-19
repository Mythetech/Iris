using System;
using System.Reflection;
using FluentAssertions;
using Iris.Api.Services.Assemblies;
using Microsoft.Extensions.DependencyInjection;

namespace Iris.Integration.Tests.Assemblies
{
    public class AssemblyServiceTests : IClassFixture<IrisWebApplicationFactory>
    {
        private const string TestAssemblyFolder = "Assemblies/TestAssemblyModules";
        private readonly IrisWebApplicationFactory _factory;
        private readonly IAssemblyService _assemblyService;

        public AssemblyServiceTests(IrisWebApplicationFactory factory)
        {
            _factory = factory;

            _assemblyService = factory.Services.GetRequiredService<IAssemblyService>();
        }

        [Theory(DisplayName = "Can load assembly")]
        [InlineData("Iris.Cloud.Demo.Contracts.dll", 5)]
        public async Task Can_LoadAssembly_FromStream(string assemblyName, int expectedCount)
        {
            // Arrange
            using var stream = File.OpenRead(Path.Combine(TestAssemblyFolder, assemblyName));
            stream.Position = 0;

            // Act
            var types = await _assemblyService.LoadAssemblyAsync(stream);

            // Assert
            types.Should().NotBeNullOrEmpty();
            types.Count.Should().Be(expectedCount);
        }

        [Theory(DisplayName = "Can retrieve cached types")]
        [InlineData("Iris.Cloud.Demo.Contracts.dll", 5)]
        public async Task Can_Retrieve_CachedTypes(string assemblyName, int expectedCount)
        {
            // Arrange
            using var stream = File.OpenRead(Path.Combine(TestAssemblyFolder, assemblyName));
            stream.Position = 0;
            var types = await _assemblyService.LoadAssemblyAsync(stream);

            // Act
            var cachedTypes = await _assemblyService.GetLoadedTypesAsync();

            // Assert
            types.Count.Should().Be(cachedTypes.Count);
            cachedTypes.Should().NotBeNullOrEmpty();
            cachedTypes.Count.Should().Be(expectedCount);
        }
        
        [Theory(DisplayName = "Can retrieve cached assemblies")]
        [InlineData("Iris.Cloud.Demo.Contracts.dll", 5)]
        public async Task Can_Retrieve_CachedAssemblies(string assemblyName, int expectedCount)
        {
            // Arrange
            using var stream = File.OpenRead(Path.Combine(TestAssemblyFolder, assemblyName));
            stream.Position = 0;
            var types = await _assemblyService.LoadAssemblyAsync(stream);

            // Act
            var cachedAssemblies = await _assemblyService.GetLoadedAssembliesAsync();

            // Assert
            types.Count.Should().Be(cachedAssemblies[0].ExportedTypes.Count());
            cachedAssemblies.Should().NotBeNullOrEmpty();
            cachedAssemblies.Count.Should().Be(1);
            cachedAssemblies[0].ExportedTypes.Count().Should().Be(expectedCount);
        }

        [Theory(DisplayName = "Can load type from assembly")]
        [InlineData("Iris.Cloud.Demo.Contracts.dll", "changecolorscommand", new string[] { "Red", "Green", "Blue" })]
        [InlineData("Iris.Cloud.Demo.Contracts.dll", "changecolorscommandv2", new string[] { "Hue", "Saturation", "Light" })]
        [InlineData("Iris.Cloud.Demo.Contracts.dll", "ISimpleMessage", new string[] { "Name", "Count", "Value", "When" })]
        [InlineData("Iris.Cloud.Demo.Contracts.dll", "SimpleMessage", new string[] { "Name", "Count", "Value", "When" })]
        [InlineData("Iris.Cloud.Demo.Contracts.dll", "SimpleRecordMessage", new string[] { "Name", "Count", "Value", "When" })]
        public async Task CanRetrieve_Type_FromAssembly(string assemblyName, string typeName, string[] propNames)
        {
            // Arrange
            using var stream = File.OpenRead(Path.Combine(TestAssemblyFolder, assemblyName));
            stream.Position = 0;
            _ = await _assemblyService.LoadAssemblyAsync(stream);

            // Act
            var type = await _assemblyService.GetTypeAsync(typeName);


            // Assert
            type.Should().NotBeNull();
            foreach (var name in propNames)
            {
                type.GetProperty(name).Should().NotBeNull();
            }
        }
    }
}