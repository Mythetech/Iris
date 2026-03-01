using Mythetech.Framework.Infrastructure.MessageBus;
using Microsoft.Extensions.Logging;

namespace Iris.Components.Test.Infrastructure;

public class SimpleServiceConsumer : IConsumer<SetText>
{
    private readonly TestDataStateService _state;

    public SimpleServiceConsumer(TestDataStateService state)
    {
        _state = state;
    }

    public string Text { get; set; } = string.Empty;
    
    public Task Consume(SetText message)
    {
        _state.Data = message.text;
        
        return Task.CompletedTask;
    }
}