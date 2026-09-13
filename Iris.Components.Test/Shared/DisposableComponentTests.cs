using System.Reflection;
using FluentAssertions;
using Iris.Components.Messaging;
using Iris.Components.Theme;
using Xunit;

namespace Iris.Components.Test.Shared;

/// <summary>
/// Blazor calls Dispose only on components that declare the interface, so a component
/// that defines Dispose() without "@implements IDisposable" unsubscribes nothing and
/// the compiler says nothing either. These are drift guards, not behaviour tests: the
/// declaration is the whole defect.
/// </summary>
public class DisposableComponentTests
{
    [Fact(DisplayName = "MessageOptions declares IDisposable so its Dispose is called")]
    public void MessageOptions_is_disposable()
    {
        // It leaked a MessageState.StateChanged subscription on every tab activation.
        typeof(MessageOptions).Should().BeAssignableTo<IDisposable>();
    }

    [Fact(DisplayName = "Iris does not carry its own copy of ProgressCountdown")]
    public void ProgressCountdown_comes_from_the_framework()
    {
        // The Iris copy had the same missing declaration, so it kept calling
        // StateHasChanged on a removed snackbar for the countdown's full duration.
        // Mythetech.Framework's version declares IDisposable and is the one in use.
        typeof(IrisIcons).Assembly.GetTypes()
            .Where(t => t.Name.StartsWith("ProgressCountdown", StringComparison.Ordinal))
            .Should().BeEmpty("the framework component replaced it");

        typeof(Mythetech.Framework.Components.Progress.ProgressCountdown)
            .Should().BeAssignableTo<IDisposable>();
    }
}
