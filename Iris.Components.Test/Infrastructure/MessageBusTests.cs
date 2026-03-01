using Bunit;
using FluentAssertions;
using Mythetech.Framework.Infrastructure.MessageBus;
using Microsoft.Extensions.DependencyInjection;

namespace Iris.Components.Test.Infrastructure;

public class MessageBusTests : TestContext
{
 public MessageBusTests()
 {
     Services.AddMessageBus(typeof(MessageBusTests).Assembly);
     Services.AddSingleton<TestDataStateService>();
 }

 [Fact(DisplayName = "Can update a simple consumer from an event")]
 public async Task Can_Update_Component()
 {
     // Arrange
     var message = new SetText("test");
     var messageBus = Services.GetService<IMessageBus>();
     var cut = RenderComponent<SimpleConsumer>();

     // Act
     await messageBus.PublishAsync(message);
     
     // Assert
     cut.MarkupMatches(@"<div>test</div>");
 }

 [Fact(DisplayName = "Can update a service consumer from an event with message")]
 public async Task Can_Update_ServiceConsumer()

 {
     // Arrange
     Services.UseMessageBus(typeof(SimpleServiceConsumer).Assembly);

     var message = new SetText("test");
     var messageBus = Services.GetService<IMessageBus>();

     // Act
     await messageBus.PublishAsync(message);

     // Assert
     // Assert
     var state = Services.GetService<TestDataStateService>();
     state.Data.Should().Be("test");
 }
 
 [Fact(DisplayName = "Can update multiple consumers from an event with message")]
 public async Task Can_Update_ServiceAndComponentConsumer()
 {
     // Arrange
     Services.UseMessageBus(typeof(SimpleServiceConsumer).Assembly);

     var message = new SetText("test2");
     var messageBus = Services.GetService<IMessageBus>();
     var cut = RenderComponent<SimpleConsumer>();

     // Act
     await messageBus.PublishAsync(message);

     // Assert
     var state = Services.GetService<TestDataStateService>();
     state.Data.Should().Be("test2");
     cut.MarkupMatches(@"<div>test2</div>");
 }
 
 [Fact(DisplayName = "No consumers should not throw exceptions")]
 public async Task No_Consumers_Should_Not_Throw()
 {
     var ctx = new TestContext();
     ctx.Services.AddMessageBus();
     ctx.Services.AddSingleton<TestDataStateService>();
     ctx.Services.UseMessageBus();
     
     // Arrange
     var message = new SetText("test");
     var messageBus = ctx.Services.GetService<IMessageBus>();

     // Act
     Func<Task> act = () => messageBus.PublishAsync(message);

     // Assert
     await act.Should().NotThrowAsync();
 }
}