using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bunit;
using FluentAssertions;
using Iris.Components.Shared.JsonTreeView;
using Microsoft.Extensions.DependencyInjection;

namespace Iris.Components.Test
{
    public class JsonTreeViewNodeTests : IrisTestContext
    {
        [Fact(DisplayName = "Can render json tree nodes with value types")]
        public void JsonTreeViewNode_ShouldRenderCorrectly_GivenFlatJsonObject()
        {
            // Arrange
            var json = "{\"stringValue\":\"test\",\"intValue\":123,\"boolValue\":true,\"dateValue\":\"2024-07-05T08:43:12.373729-04:00\"}";
            var node = JsonNode.Parse(json);

            // Act
            var cut = Render<JsonTreeViewNode>(parameters => parameters
                .Add(p => p.Node, node)
            );

            // Assert
            var treeViewItems = cut.FindAll(".mud-treeview-item");
            treeViewItems.Should().HaveCount(4);

            treeViewItems[0].InnerHtml.Should().Contain(">text_fields<");
            treeViewItems[1].InnerHtml.Should().Contain(">tag<");
            treeViewItems[2].InnerHtml.Should().Contain(">check<");
            treeViewItems[3].InnerHtml.Should().Contain(">calendar_today<");
        }
    }
}

