namespace Iris.Components.Theme;

public class IrisAppState
{
    public ThemeMode ThemeMode { get; set; } = ThemeMode.System;

    public bool IsDarkMode { get; set; } = true;
}
