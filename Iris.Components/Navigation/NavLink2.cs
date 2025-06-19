using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Routing;

namespace Iris.Components.Navigation;

/// <summary>
/// A component that renders an anchor tag, automatically toggling its 'active'
/// class based on whether its 'href' matches the current URI, and provides
/// access to the active state for child components through context.
/// </summary>
public class NavLink2 : ComponentBase, IDisposable
{
    private const string DefaultActiveClass = "active";

    private bool _isActive;
    private string? _hrefAbsolute;
    private string? _class;

    /// <summary>
    /// Gets or sets the CSS class name applied to the NavLink when the
    /// current route matches the NavLink href.
    /// </summary>
    [Parameter]
    public string? ActiveClass { get; set; }

    /// <summary>
    /// Gets or sets a collection of additional attributes that will be added to the generated
    /// <c>a</c> element.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// Gets or sets the computed CSS class based on whether or not the link is active.
    /// </summary>
    protected string? CssClass { get; set; }

    /// <summary>
    /// Gets or sets the child content of the component. It will receive the active state as context.
    /// </summary>
    [Parameter]
    public RenderFragment<bool>? ChildContent { get; set; }

    /// <summary>
    /// Gets or sets a value representing the URL matching behavior.
    /// </summary>
    [Parameter]
    public NavLinkMatch Match { get; set; }

    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        // We'll consider re-rendering on each location change
        NavigationManager.LocationChanged += OnLocationChanged;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // Update computed state
        var href = (string?)null;
        if (AdditionalAttributes != null && AdditionalAttributes.TryGetValue("href", out var obj))
        {
            href = Convert.ToString(obj, CultureInfo.InvariantCulture);
        }

        _hrefAbsolute = href == null ? null : NavigationManager.ToAbsoluteUri(href).AbsoluteUri;
        _isActive = ShouldMatch(NavigationManager.Uri);

        _class = (string?)null;
        if (AdditionalAttributes != null && AdditionalAttributes.TryGetValue("class", out obj))
        {
            _class = Convert.ToString(obj, CultureInfo.InvariantCulture);
        }

        UpdateCssClass();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // To avoid leaking memory, it's important to detach any event handlers in Dispose()
        NavigationManager.LocationChanged -= OnLocationChanged;
    }

    private void UpdateCssClass()
    {
        CssClass = _isActive ? CombineWithSpace(_class, ActiveClass ?? DefaultActiveClass) : _class;
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        var shouldBeActiveNow = ShouldMatch(args.Location);
        if (shouldBeActiveNow != _isActive)
        {
            _isActive = shouldBeActiveNow;
            UpdateCssClass();
            StateHasChanged();
        }
    }

    private bool ShouldMatch(string currentUriAbsolute)
    {
        if (_hrefAbsolute == null)
        {
            return false;
        }

        if (EqualsHrefExactlyOrIfTrailingSlashAdded(currentUriAbsolute))
        {
            return true;
        }

        if (Match == NavLinkMatch.Prefix
            && IsStrictlyPrefixWithSeparator(currentUriAbsolute, _hrefAbsolute))
        {
            return true;
        }

        return false;
    }

    private bool EqualsHrefExactlyOrIfTrailingSlashAdded(string currentUriAbsolute)
    {
        Debug.Assert(_hrefAbsolute != null);

        if (string.Equals(currentUriAbsolute, _hrefAbsolute, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (currentUriAbsolute.Length == _hrefAbsolute.Length - 1)
        {
            if (_hrefAbsolute[_hrefAbsolute.Length - 1] == '/'
                && _hrefAbsolute.StartsWith(currentUriAbsolute, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "a");

        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "class", CssClass);
        if (_isActive)
        {
            builder.AddAttribute(3, "aria-current", "page");
        }

        // Render the child content, passing the _isActive state as the context
        if (ChildContent != null)
        {
            builder.AddContent(4, ChildContent(_isActive));
        }

        builder.CloseElement();
    }

    private static string? CombineWithSpace(string? str1, string str2)
        => str1 == null ? str2 : $"{str1} {str2}";

    private static bool IsStrictlyPrefixWithSeparator(string value, string prefix)
    {
        var prefixLength = prefix.Length;
        if (value.Length > prefixLength)
        {
            return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                   && (
                       prefixLength == 0
                       || !IsUnreservedCharacter(prefix[prefixLength - 1])
                       || !IsUnreservedCharacter(value[prefixLength])
                   );
        }
        else
        {
            return false;
        }
    }

    private static bool IsUnreservedCharacter(char c)
    {
        return char.IsLetterOrDigit(c) ||
               c == '-' ||
               c == '.' ||
               c == '_' ||
               c == '~';
    }
}