namespace Iris.Components.Infrastructure.MessageBus;

public class InMemoryMessageBus : IMessageBus
{
    private readonly Dictionary<Type, List<Type>> _registeredConsumerTypes = new();
    private readonly Dictionary<Type, List<object>> _cachedConsumers = new();
    private readonly Dictionary<Type, List<object>> _subscribers = new();

    private readonly IServiceProvider _serviceProvider;

    public InMemoryMessageBus(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task PublishAsync<TMessage>(TMessage message) where TMessage : class
    {
        var registeredConsumers = GetOrResolveConsumers<TMessage>();

        var manualSubscribers = _subscribers.TryGetValue(typeof(TMessage), out var subscribers)
            ? subscribers.Cast<IConsumer<TMessage>>()
            : Enumerable.Empty<IConsumer<TMessage>>();

        var allConsumers = registeredConsumers.Concat(manualSubscribers);

        var tasks = allConsumers.Select(consumer => consumer.Consume(message));
        return Task.WhenAll(tasks);
    }

    public void RegisterConsumerType<TMessage, TConsumer>() where TMessage : class where TConsumer : IConsumer<TMessage>
    {
        if (!_registeredConsumerTypes.ContainsKey(typeof(TMessage)))
        {
            _registeredConsumerTypes[typeof(TMessage)] = new List<Type>();
        }

        _registeredConsumerTypes[typeof(TMessage)].Add(typeof(TConsumer));
    }

    private List<IConsumer<TMessage>> GetOrResolveConsumers<TMessage>() where TMessage : class
    {
        var messageType = typeof(TMessage);

        if (!_cachedConsumers.TryGetValue(messageType, out var cached))
        {
            if (!_registeredConsumerTypes.TryGetValue(messageType, out var consumerTypes))
                return new List<IConsumer<TMessage>>(); // No consumers registered

            cached = consumerTypes
                .Select(type => _serviceProvider.GetService(type))
                .Where(consumer => consumer is not null)
                .ToList();

            _cachedConsumers[messageType] = cached;
        }

        return cached.Cast<IConsumer<TMessage>>().ToList();
    }

    public void Subscribe<TMessage>(IConsumer<TMessage> consumer) where TMessage : class
    {
        if (!_subscribers.ContainsKey(typeof(TMessage)))
            _subscribers[typeof(TMessage)] = new List<object>();

        _subscribers[typeof(TMessage)].Add(consumer);
    }

    public void Unsubscribe<TMessage>(IConsumer<TMessage> consumer) where TMessage : class
    {
        if (_subscribers.TryGetValue(typeof(TMessage), out var handlers))
        {
            handlers.Remove(consumer);
            if (handlers.Count == 0)
                _subscribers.Remove(typeof(TMessage));
        }
    }
}