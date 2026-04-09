using MudBlazor;

namespace Iris.Components.NativeMenu.Commands;

public record ShowDialog(Type Dialog, string Title, DialogOptions? Options = default, DialogParameters? Parameters = default);
