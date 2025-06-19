using Bunit;
using FluentAssertions;
using Iris.Components.Settings;

namespace Iris.Components.Test.Settings;

public class SectionTests : TestContext
{
    public SectionTests()
    {
        
    }

    [Fact(DisplayName = "Can render basic section layout")]
    public void Can_Render_SectionLayout()
    {
        // Arrange
        var l = new SimpleSection();
        var cut = RenderComponent<SectionLayout>(p => p.Add(x => x.Section, l));
        
        // Act
        var text = cut.Find("p");
        
        // Assert
        cut.Should().NotBeNull();
        text.MarkupMatches("<p>test</p>");
    }
    
    [Fact(DisplayName = "Can render complex section layout")]
    public void Can_Render_ComplexSectionLayout()
    {
        // Arrange
        var l = RenderComponent<ComplexSection>();
        var cut = RenderComponent<SectionLayout>(p => p.Add(x => x.Section, l.Instance));
        
        // Act
        var text = cut.FindAll("p");
        
        // Assert
        cut.Should().NotBeNull();
        text.Count.Should().Be(2);
        text[1].MarkupMatches("<p>test</p>");
            
        text[0].MarkupMatches("<p>basic</p>");
    }
    
}