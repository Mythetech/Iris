using Iris.Components.Messaging;
using Iris.Contracts.Brokers.Models;
using Mythetech.Framework.Infrastructure.MessageBus;
using Mythetech.Framework.Infrastructure.Settings.Events;

public class MessageState : IDisposable
{
    private const string IrisHeaderKey = "iris-key";

    private const string DefaultIrisKeyMessage = "{{ Generated on Send }}";

    private readonly MessagingSettings _settings;
    private readonly IMessageBus _bus;
    private readonly SettingsSubscription _settingsSubscription;

    private readonly DictionaryViewModel _irisKeyHeader = new()
    {
        Key = IrisHeaderKey,
        Value = DefaultIrisKeyMessage,
        Immutable = true
    };

    public MessageState(MessagingSettings settings, IMessageBus bus)
    {
        _settings = settings;
        _bus = bus;
        _settingsSubscription = new SettingsSubscription(this);
        _bus.Subscribe(_settingsSubscription);
        SyncIrisHeader();
    }

    public int MaxDelay { get; set; } = 60;
    public bool Sending { get; set; }
    public int Delay { get; set; }
    public string DelayText { get; private set; } = string.Empty;
    public int Repeat { get; set; }
    public string RepeatText { get; private set; } = string.Empty;

    public string? SelectedFramework { get; private set; }

    /// <summary>
    /// Read straight off the settings model rather than mirrored into a field, so the
    /// toggle in the settings dialog cannot drift from what actually ships on a send.
    /// </summary>
    public bool SendIrisHeader => _settings.SendIrisHeader;

    public List<DictionaryViewModel> AdditionalProperties { get; private set; } = new()
    {
        new DictionaryViewModel
        {
            Key = "MessageType",
            Value = "",
            Description = "Explicitly overrides the message type. Required for some frameworks."
        }
    };

    public Dictionary<string, string> GetFrameworkProperties()
    {
        return AdditionalProperties.ToDictionary(p => p.Key, p => p.Value);
    }

    public List<DictionaryViewModel> HeaderMap { get; set; } = new();

    private void NotifyStateChanged()
        => StateChanged?.Invoke();

    public Dictionary<string, string> Headers { get; private set; } = new();

    public event Action? StateChanged;

    public void SetDelay(int delay)
    {
        Delay = delay;
        StateChanged?.Invoke();
    }

    public void SetRepeat(int repeat)
    {
        Repeat = repeat;
        StateChanged?.Invoke();
    }

    public void SetRepeatText(string text)
    {
        RepeatText = text;
        NotifyStateChanged();
    }

    public void SetDelayText(string text)
    {
        DelayText = text;
        NotifyStateChanged();
    }

    public void SetSending(bool sending)
    {
        Sending = sending;
        StateChanged?.Invoke();
    }

    public void SetFramework(string? framework)
    {
        SelectedFramework = framework;
        StateChanged?.Invoke();
    }
    
    public void AddHeader(DictionaryViewModel newHeader)
    {
        HeaderMap.Add(newHeader);
        StateChanged?.Invoke();
    }

    public void RemoveHeader(DictionaryViewModel header)
    {
        HeaderMap.Remove(header);
        StateChanged?.Invoke();
    }
    
    public void AddAdditionalProperty(DictionaryViewModel newProperty)
    {
        AdditionalProperties.Add(newProperty);
        StateChanged?.Invoke();
    }

    public void RemoveAdditionalProperty(DictionaryViewModel property)
    {
        AdditionalProperties.Remove(property);
        StateChanged?.Invoke();
    }

    public Dictionary<string, string> GetAdditionalProps()
    {
        return AdditionalProperties.ToDictionary(x => x.Key ?? "", y => y.Value ?? "");
    }

    public Dictionary<string, string> GetHeaders()
    {
        return HeaderMap.ToDictionary(x => x.Key ?? "", y => y.Value ?? "");
    }

    public void KeyChanged(string key, string newKey)
    {
        if (Headers.ContainsKey(key))
        {
            Headers[newKey] = Headers[key];
            Headers.Remove(key);
            StateChanged?.Invoke();
        }
    }

    public void ValueChanged(string key, string newValue)
    {
        if (Headers.ContainsKey(key))
        {
            Headers[key] = newValue;
            StateChanged?.Invoke();
        }
    }

    public void RegenerateIrisKey()
    {
        var m = HeaderMap.FirstOrDefault(x => x.Key == IrisHeaderKey);
        if (m != null)
        {
            m.Value = Guid.NewGuid().ToString();
        }
        else
        {
            HeaderMap.Add(new DictionaryViewModel { Key = IrisHeaderKey, Value = Guid.NewGuid().ToString() });
        }
        StateChanged?.Invoke();
    }

    public void SetEndpointMetadata(EndpointDetails? selectedEndpoint)
    {
        if(!AdditionalProperties.Any(x => x.Key.Equals("EndpointType")))
            AdditionalProperties.Add(new DictionaryViewModel { Key = "EndpointType", Value = selectedEndpoint.Type });
    }

    /// <summary>
    /// Keeps the generated-on-send placeholder row in the headers grid aligned with the
    /// setting. Only the row this state owns is added or removed, so a user header that
    /// happens to start with "iris-" survives the toggle.
    /// </summary>
    private void SyncIrisHeader()
    {
        if (SendIrisHeader)
        {
            if (!HeaderMap.Contains(_irisKeyHeader))
            {
                HeaderMap.Insert(0, _irisKeyHeader);
            }
        }
        else
        {
            HeaderMap.Remove(_irisKeyHeader);
        }
    }

    public void Dispose()
    {
        _bus.Unsubscribe(_settingsSubscription);
    }

    /// <summary>
    /// Bridges the settings event onto this state without making MessageState itself an
    /// IConsumer: assembly scanning would register the scoped state as a root-resolved
    /// consumer type and hand the bus a different instance than the UI holds.
    /// </summary>
    private sealed class SettingsSubscription : IConsumer<SettingsModelChanged<MessagingSettings>>
    {
        private readonly MessageState _state;

        public SettingsSubscription(MessageState state)
        {
            _state = state;
        }

        public Task Consume(SettingsModelChanged<MessagingSettings> message)
        {
            _state.SyncIrisHeader();
            _state.NotifyStateChanged();
            return Task.CompletedTask;
        }
    }
}