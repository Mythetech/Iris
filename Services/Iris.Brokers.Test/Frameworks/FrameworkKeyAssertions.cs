using FluentAssertions;
using Iris.Brokers.Frameworks;
using Iris.Brokers.Models;

namespace Iris.Brokers.Test.Frameworks;

/// <summary>
/// Drift guard shared by every adapter test: the keys an adapter declares must be
/// exactly the headers and transport properties it writes, so the compatibility
/// check and the UI never reason about a key the wire never sees.
/// </summary>
public static class FrameworkKeyAssertions
{
    public static void AssertKeysMatchWrite(IFramework framework, MessageRequest request)
    {
        request.Headers.Should().BeEmpty("the request must start with no headers so the comparison is exact");
        request.TransportProperties.SetProperties().Should().BeEmpty();

        framework.CreateWrappedMessage(request);

        var declaredHeaders = framework.Keys
            .Where(k => k.Location == KeyLocation.Header)
            .Select(k => k.Name);
        request.Headers.Keys.Should().BeEquivalentTo(declaredHeaders);

        var declaredProperties = framework.Keys
            .Where(k => k.Location == KeyLocation.TransportProperty)
            .Select(k => k.Property!.Value);
        request.TransportProperties.SetProperties().Should().BeEquivalentTo(declaredProperties);

        // FluentAssertions' OnlyContain fails on an empty sequence by design, and several
        // adapters declare zero header or zero transport-property keys, so the invariant is
        // checked over the full (always non-empty) Keys list with an equivalent predicate
        // rather than over a filtered subset that can legitimately be empty.
        framework.Keys.Should().OnlyContain(
            k => k.Location != KeyLocation.TransportProperty || k.Property.HasValue,
            "transport keys carry the enum member they map to");
        framework.Keys.Should().OnlyContain(
            k => k.Location == KeyLocation.TransportProperty || !k.Property.HasValue);
    }
}
