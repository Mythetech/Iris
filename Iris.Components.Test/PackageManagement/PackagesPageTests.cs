using Bunit;
using FluentAssertions;
using Iris.Components.PackageManagement;
using Iris.Contracts.Assemblies.Models;
using Iris.Contracts.Results;
using Microsoft.Extensions.DependencyInjection;
using Mythetech.Framework.Infrastructure.Files;
using NSubstitute;
using Xunit;

namespace Iris.Components.Test.PackageManagement;

public class PackagesPageTests : IrisTestContext
{
    private readonly IPackageService _packages = Substitute.For<IPackageService>();
    private readonly IFileOpenService _files = Substitute.For<IFileOpenService>();

    public PackagesPageTests()
    {
        Services.AddSingleton(_packages);
        Services.AddSingleton(_files);

        AddPopoverProvider();
    }

    private static AssemblyData Uploaded() => new()
    {
        Name = "Contracts",
        FullyQualifiedName = "Contracts, Version=1.0.0.0",
        ExportedTypes = [new TypeData { Name = "OrderPlaced", FullyQualifiedName = "Contracts.OrderPlaced" }],
    };

    [Fact(DisplayName = "An upload that lands before the initial load still shows up")]
    public async Task Upload_before_the_first_load_completes()
    {
        // Assemblies and Types are only assigned in OnInitializedAsync, so an upload that
        // finished first threw NullReferenceException out of the upload handler and the
        // package silently never appeared.
        var firstLoad = new TaskCompletionSource<List<AssemblyData>>();
        _packages.GetLoadedAssembliesAsync().Returns(firstLoad.Task);

        _files.OpenFileAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<FileFilter[]?>())
            .Returns(["/tmp/Contracts.dll"]);
        _packages.UploadAssemblyAsync(Arg.Any<string>())
            .Returns(new Success<AssemblyData>(Uploaded()));

        var cut = RenderComponent<Packages>();

        await cut.Find("button").ClickAsync(new());

        cut.Markup.Should().Contain("Contracts");

        firstLoad.SetResult([]);
    }
}
