using System;
using System.Reflection.Metadata;
using Bunit;
using FluentAssertions;
using Iris.Components.Messaging;
using Iris.Components.Shared;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace Iris.Components.Test.Messaging
{
    public class MessageHeadersTests : IrisTestContext
    {
        public MessageHeadersTests()
        {
            Services.AddScoped<MessageState>();

            JSInterop.Setup<int>("mudpopoverHelper.countProviders", _ => true);
            RenderComponent<MudPopoverProvider>();
        }

        [Fact(DisplayName = "Can add to the list with the button")]
        public async Task CanAddToListWithButton()
        {
            // Arrange
            var cut = RenderComponent<MessageHeaders>();

            // Act
            var btn = cut.FindComponent<MudButton>();

            await btn.InvokeAsync(btn.Instance.OnClick.InvokeAsync);

            // Assert
            cut.Instance.MessageState.HeaderMap.Should().HaveCountGreaterOrEqualTo(1);
            cut.Instance.MessageState.HeaderMap.Last().Key.Should().Be("Untitled");
            cut.Instance.MessageState.HeaderMap.Last().Value.Should().Be("");
        }

        [Fact(DisplayName = "List maps back to a dictionary")]
        public void ListMapsBackToDictionary()
        {
            // Arrange
            var cut = RenderComponent<MessageHeaders>();

            cut.Instance.MessageState.HeaderMap.Add(new DictionaryViewModel { Key = "Key1", Value = "Value1" });
            cut.Instance.MessageState.HeaderMap.Add(new DictionaryViewModel { Key = "Key2", Value = "Value2" });

            // Act
            var dictionary = cut.Instance.MessageState.GetHeaders();

            // Assert
            dictionary.Should().HaveCountGreaterOrEqualTo(2);
            dictionary.Should().ContainKey("Key1").WhoseValue.Should().Be("Value1");
            dictionary.Should().ContainKey("Key2").WhoseValue.Should().Be("Value2");
        }

        [Fact(DisplayName = "Quick filter logic filters dictionary")]
        public async Task QuickFilter_Filters_Dictionary()
        {
            // Arrange
            var cut = RenderComponent<MessageHeaders>();

            cut.Instance.MessageState.HeaderMap.Add(new DictionaryViewModel { Key = "Key1", Value = "Value1" });
            cut.Instance.MessageState.HeaderMap.Add(new DictionaryViewModel { Key = "Key2", Value = "Value2" });
            var search = cut.FindComponent<MudTextField<string>>();
            await search.InvokeAsync(() => search.Instance.SetTextAsync("key1"));

            // Act
            var filteredItems = cut.FindComponent<MudDataGrid<DictionaryViewModel>>();


            // Assert
            filteredItems.Instance.FilteredItems.Should().HaveCountGreaterOrEqualTo(1);
            filteredItems.Instance.FilteredItems.Last().Key.Should().Be("Key1");
            filteredItems.Instance.FilteredItems.Last().Value.Should().Be("Value1");
        }
        
        [Fact(DisplayName = "Respects immutable dictionary records")]
        public void Can_Not_DeleteImmutableRecords()
        {
            // Arrange
            var cut = RenderComponent<MessageHeaders>();

            cut.Instance.MessageState.HeaderMap.Add(new DictionaryViewModel { Key = "Key1", Value = "Value1", Immutable = true });
            

            // Act
            var button = cut.FindComponent<IconButton>();


            // Assert
            button.Instance.Disabled.Should().BeTrue();
        }
    }
}