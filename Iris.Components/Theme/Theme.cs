using System;
using MudBlazor;

namespace Iris.Components.Theme
{
    public class Theme
    {
        public static MudTheme Iris { get; } = new()
        {
            PaletteLight = new PaletteLight()
            {
                Primary = Colors.Blue.Default,
                Secondary = Colors.Green.Accent4,
                AppbarBackground = Colors.Red.Default,
                Background = Colors.Shades.White,
                DrawerBackground = Colors.Shades.White,
                Surface = Colors.Shades.White,

            },
            PaletteDark = new PaletteDark()
            {
                Primary = Colors.Blue.Lighten1,
                Secondary = Colors.Purple.Lighten1,
                Tertiary = Colors.Cyan.Lighten1,
                Background = Colors.Shades.Black,
                DrawerBackground = Colors.Shades.Black,
                Surface = Colors.Shades.Black,
                GrayDarker = "#222"
            },
            LayoutProperties = new LayoutProperties()
            {
                DefaultBorderRadius = "0.5em",
                DrawerWidthLeft = "20vw",
                DrawerWidthRight = "30vw",
            },
            Typography = new Typography()
            {
                Default = new DefaultTypography()
                {
                    FontFamily = new[] { "Helvetica-Neue", "Helvetica", "Arial", "sans-serif" },
                    TextTransform = "none",
                },
                Button = new ButtonTypography()
                {
                    FontFamily = new[] { "Helvetica-Neue", "Helvetica", "Arial", "sans-serif" },
                    TextTransform = "none",
                }
            }
        };
    }
}

