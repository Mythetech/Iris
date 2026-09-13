using Bunit;
using FluentAssertions;
using Iris.Components.Shared.JsonTreeView;
using Xunit;

namespace Iris.Components.Test.Shared;

public class JsonTreeViewTests : IrisTestContext
{
    [Fact(DisplayName = "Re-parses when the parent hands it different JSON")]
    public void Reparses_on_a_new_json_parameter()
    {
        // The tree parsed only in OnInitialized. Blazor reuses a component instance when
        // the same position in the render tree gets new parameters, so the message list in
        // MessageReaderDialog showed the first batch's bodies after every later read.
        var cut = RenderComponent<JsonTreeView>(p => p.Add(x => x.Json, """{"orderId":"first"}"""));

        cut.Markup.Should().Contain("first");

        cut.SetParametersAndRender(p => p.Add(x => x.Json, """{"orderId":"second"}"""));

        cut.Markup.Should().Contain("second").And.NotContain("first");
    }
}
