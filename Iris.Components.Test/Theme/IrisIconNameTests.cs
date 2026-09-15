using System.Reflection;
using FluentAssertions;
using Iris.Components.Theme;

namespace Iris.Components.Test.Theme;

/// <summary>
/// Material Symbols resolves an icon by ligature: the browser is handed the icon name as
/// text and the font substitutes a glyph. A name the font does not carry has no glyph to
/// substitute, so the name renders as literal text in the UI and nothing fails at build
/// time. Six constants had drifted onto Font Awesome and Lucide names that way.
///
/// Fixtures/material-symbols-rounded.txt is the ligature table extracted from the woff2 the
/// app actually loads, so this catches the drift the compiler cannot.
/// </summary>
public class IrisIconNameTests
{
    private static readonly HashSet<string> Ligatures = LoadLigatures();

    private static HashSet<string> LoadLigatures()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "material-symbols-rounded.txt");
        return File.ReadLines(path)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IEnumerable<(string Name, string Value)> Icons()
    {
        foreach (var (owner, type) in new[]
                 {
                     (nameof(IrisIcons), typeof(IrisIcons)),
                     ($"{nameof(IrisIcons)}.{nameof(IrisIcons.Types)}", typeof(IrisIcons.Types)),
                 })
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Static)
                         .Where(p => p.PropertyType == typeof(string)))
                yield return ($"{owner}.{property.Name}", (string)property.GetValue(null)!);
    }

    public static TheoryData<string, string> IconProperties()
    {
        var data = new TheoryData<string, string>();

        foreach (var (name, value) in Icons())
            data.Add(name, value);

        return data;
    }

    [Theory]
    [MemberData(nameof(IconProperties))]
    public void Every_icon_resolves_to_a_glyph_in_the_shipped_font(string name, string value)
    {
        // The inline SVG fallbacks are not ligatures, and Rounded is the prefix itself.
        if (!value.StartsWith(IrisIcons.Rounded, StringComparison.Ordinal))
            return;

        var ligature = value[IrisIcons.Rounded.Length..];
        if (ligature.Length == 0)
            return;

        Ligatures.Should().Contain(ligature,
            $"{name} renders as the literal text \"{ligature}\" unless the font carries that ligature");
    }

    [Fact]
    public void The_ligature_fixture_was_loaded()
    {
        Ligatures.Should().HaveCountGreaterThan(3000);
    }

    [Fact]
    public void Every_icon_is_a_material_symbol_or_an_inline_svg()
    {
        var strays = Icons()
            .Where(x => !x.Value.StartsWith('<')
                        && !x.Value.StartsWith(IrisIcons.Rounded, StringComparison.Ordinal)
                        && x.Name != $"{nameof(IrisIcons)}.{nameof(IrisIcons.Rounded)}")
            .Select(x => x.Name);

        strays.Should().BeEmpty();
    }
}
