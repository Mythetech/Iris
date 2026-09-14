using System.Reflection;
using FluentAssertions;

namespace Iris.Integration.Tests;

/// <summary>
/// Every test class in this assembly has to say whether it needs Docker.
///
/// <para>
/// CI runs the whole solution twice: once with <c>--filter-not-trait "Category=Container"</c>
/// on every platform, and once with <c>--filter-trait "Category=Container"</c> on Linux, where
/// Docker is. Most of this project belongs in the second run, but not all of it, and the split
/// is decided entirely by this trait. A class that carries neither value ends up in the fast
/// job, where it will try to start a container and fail on a machine that has no daemon.
/// </para>
///
/// <para>
/// Hence a guard rather than an allow-list: a new class has to choose, and the choice is
/// visible in the file it belongs to.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class TestCategoryTests
{
    [Fact(DisplayName = "Every test class in the integration assembly declares a known category")]
    public void Test_classes_declare_a_category()
    {
        var uncategorized = typeof(TestCategoryTests).Assembly.GetTypes()
            .Where(HasTests)
            .Where(type => CategoryOf(type) is not (TestCategories.Container or TestCategories.Unit))
            .Select(type => type.FullName)
            .ToList();

        uncategorized.Should().BeEmpty(
            $"every test class here needs [Trait(\"Category\", ...)] naming "
            + $"\"{TestCategories.Container}\" or \"{TestCategories.Unit}\"");
    }

    private static bool HasTests(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Any(method => method.GetCustomAttributes()
                .Any(attribute => attribute is FactAttribute or TheoryAttribute));

    /// <summary>
    /// Read off the attribute's constructor arguments rather than properties: xUnit's
    /// <c>TraitAttribute</c> exposes none.
    /// </summary>
    private static string? CategoryOf(Type type) =>
        type.GetCustomAttributesData()
            .Where(data => data.AttributeType == typeof(TraitAttribute) && data.ConstructorArguments.Count == 2)
            .Where(data => (string?)data.ConstructorArguments[0].Value == "Category")
            .Select(data => (string?)data.ConstructorArguments[1].Value)
            .FirstOrDefault();
}

/// <summary>
/// The two answers to "does this test need Docker". Constants rather than literals so a typo
/// is a compile error instead of a test that quietly runs in the wrong CI job.
/// </summary>
public static class TestCategories
{
    /// <summary>Needs a Docker daemon. Runs only in the Linux integration job.</summary>
    public const string Container = "Container";

    /// <summary>Needs nothing. Runs in the fast cross-platform job with the unit tests.</summary>
    public const string Unit = "Unit";
}
