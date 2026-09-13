using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Iris.Assemblies.Test;

public class AssemblyLoaderTests : IClassFixture<EmittedAssemblyFixture>
{
    private readonly EmittedAssemblyFixture _fixture;
    private readonly AssemblyLoader _loader = new(NullLogger<AssemblyLoader>.Instance);

    public AssemblyLoaderTests(EmittedAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "A contracts assembly whose dependency sits beside it maps without error")]
    public async Task Resolves_sibling_dependency_from_the_assembly_directory()
    {
        var path = _fixture.StageContracts(withDependency: true);

        var loaded = await LoadAsync(path);

        loaded.Should().NotBeNull();

        // Mapping is the step that reflects over every property type, so it is the step that
        // used to throw FileNotFoundException and leave the Packages page broken until restart.
        var contract = loaded!.Assembly.ToContract();

        var message = contract.ExportedTypes!.Single(t => t.Name == "OrderPlaced");
        message.Properties!.Select(p => p.Name).Should().BeEquivalentTo("Reference", "ShipTo");
        message.Properties!.Single(p => p.Name == "ShipTo").TypeName.Should().Be("Address");

        loaded.Context.Unload();
    }

    [Fact(DisplayName = "Without the dependency the failure still happens at mapping, not at load")]
    public async Task Reports_the_missing_dependency_when_it_is_not_beside_the_assembly()
    {
        var path = _fixture.StageContracts(withDependency: false);

        var loaded = await LoadAsync(path);

        // Loading has always succeeded here. The point of the fix is not to make this case work,
        // it is that the caller gets the failure while it can still throw the assembly away.
        loaded.Should().NotBeNull();

        var mapping = () => loaded!.Assembly.ToContract();
        mapping.Should().Throw<Exception>();

        loaded!.Context.Unload();
    }

    [Fact(DisplayName = "A stream with no path behaves as it always did")]
    public async Task Loads_without_a_path()
    {
        await using var stream = File.OpenRead(_fixture.ContractsPath);

        var loaded = await _loader.LoadAssemblyAsync(stream);

        loaded.Should().NotBeNull();
        loaded!.Assembly.GetName().Name.Should().Be(EmittedAssemblyFixture.ContractsName);
        loaded.Context.IsCollectible.Should().BeTrue();

        loaded.Context.Unload();
    }

    [Fact(DisplayName = "Garbage is rejected rather than thrown out of the loader")]
    public async Task Returns_null_for_a_file_that_is_not_an_assembly()
    {
        using var stream = new MemoryStream("not an assembly"u8.ToArray());

        var loaded = await _loader.LoadAssemblyAsync(stream, "nonsense.dll");

        loaded.Should().BeNull();
    }

    private async Task<LoadedAssembly?> LoadAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        return await _loader.LoadAssemblyAsync(stream, path);
    }
}
