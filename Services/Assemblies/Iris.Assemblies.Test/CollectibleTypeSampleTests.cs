using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Iris.Assemblies.Test;

/// <summary>
/// Pins the replacement for the deleted CodeGenerator. Sample bodies used to come from a
/// dynamic clone of the user's type defined with <c>AssemblyBuilderAccess.Run</c>, which is
/// non-collectible, so cloning a type out of the collectible context Iris loads packages into
/// threw as soon as one of its properties was itself user-defined. These tests run the real
/// path, on a real type in a real collectible context, with exactly that shape.
/// </summary>
public class CollectibleTypeSampleTests : IClassFixture<EmittedAssemblyFixture>
{
    private readonly EmittedAssemblyFixture _fixture;

    public CollectibleTypeSampleTests(EmittedAssemblyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "A collectible type with a user-defined property produces a sample body")]
    public async Task Samples_a_type_loaded_into_a_collectible_context()
    {
        var path = _fixture.StageContracts(withDependency: true);
        await using var stream = File.OpenRead(path);

        var loaded = await new AssemblyLoader(NullLogger<AssemblyLoader>.Instance)
            .LoadAssemblyAsync(stream, path);

        loaded.Should().NotBeNull();
        loaded!.Context.IsCollectible.Should().BeTrue();

        var type = loaded.Assembly.GetType(EmittedAssemblyFixture.MessageTypeName);
        type.Should().NotBeNull();

        var sample = new SampleJsonGenerator().GenerateSample(type!.ToContract());

        sample["Reference"]!.GetValue<string>().Should().Be("sample-string");

        // The nested user-defined type is the case that never worked. It has to come back as an
        // object with its own properties filled in, not as an empty node and not as a throw.
        sample["ShipTo"].Should().BeOfType<JsonObject>();
        sample["ShipTo"]!["City"]!.GetValue<string>().Should().Be("sample-string");

        loaded.Context.Unload();
    }
}
