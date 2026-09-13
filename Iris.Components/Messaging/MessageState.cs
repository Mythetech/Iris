using Iris.Components.Messaging;
using Iris.Contracts.Assemblies.Models;
using Iris.Contracts.Brokers.Models;
using Iris.Contracts.Messaging.Frameworks;
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

    public FrameworkDescriptor? SelectedFrameworkDescriptor { get; private set; }

    public string? SelectedFramework => SelectedFrameworkDescriptor?.Name;

    public IReadOnlyList<FrameworkDescriptor> AvailableFrameworks { get; private set; } = [];

    /// <summary>Why the last selection was cleared, shown under the selector until the next change.</summary>
    public string? FrameworkNotice { get; private set; }

    /// <summary>
    /// Read straight off the settings model rather than mirrored into a field, so the
    /// toggle in the settings dialog cannot drift from what actually ships on a send.
    /// </summary>
    public bool SendIrisHeader => _settings.SendIrisHeader;

    public List<DictionaryViewModel> AdditionalProperties { get; private set; } = new();

    public Dictionary<string, string> GetFrameworkProperties()
    {
        var properties = new Dictionary<string, string>();
        foreach (var row in AdditionalProperties)
        {
            if (string.IsNullOrWhiteSpace(row.Key))
                continue;
            properties[row.Key] = row.Value ?? string.Empty;
        }
        return properties;
    }

    public List<DictionaryViewModel> HeaderMap { get; set; } = new();

    private void NotifyStateChanged()
        => StateChanged?.Invoke();

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

    public void SetAvailableFrameworks(IReadOnlyList<FrameworkDescriptor> frameworks)
    {
        AvailableFrameworks = frameworks;

        var current = SelectedFramework is null
            ? null
            : frameworks.FirstOrDefault(f => f.Name == SelectedFramework);

        if (SelectedFramework is not null && current is not { Supported: true })
        {
            FrameworkNotice = current?.UnsupportedReason ?? $"{SelectedFramework} is not available for this connection.";
            SetFramework(null);
            return;
        }

        NotifyStateChanged();
    }

    public void SetFramework(FrameworkDescriptor? descriptor)
    {
        var previousKeys = SelectedFrameworkDescriptor?.Inputs.Select(i => i.Key).ToHashSet() ?? new HashSet<string>();
        var nextInputs = descriptor?.Inputs ?? [];
        var nextKeys = nextInputs.Select(i => i.Key).ToHashSet();

        AdditionalProperties.RemoveAll(row =>
            row.Key is not null && previousKeys.Contains(row.Key) && !nextKeys.Contains(row.Key));

        foreach (var input in nextInputs)
        {
            var row = AdditionalProperties.FirstOrDefault(r => r.Key == input.Key);
            if (row is null)
            {
                AdditionalProperties.Add(new DictionaryViewModel
                {
                    Key = input.Key,
                    Value = input.DefaultValue ?? string.Empty,
                    Description = input.Description,
                    AllowedValues = input.AllowedValues,
                    Required = input.Required,
                });
            }
            else
            {
                row.Description = input.Description;
                row.AllowedValues = input.AllowedValues;
                row.Required = input.Required;
            }
        }

        SelectedFrameworkDescriptor = descriptor;
        if (descriptor is not null)
            FrameworkNotice = null;

        NotifyStateChanged();
    }

    /// <summary>
    /// Fills the type-related rows the selected framework declares from a picked type.
    /// Rows the framework does not declare are never added, and a blank assembly name
    /// leaves the row for the send path to resolve from loaded packages.
    /// </summary>
    public void SeedTypeInputs(TypeData type, string? assemblyName)
    {
        SetRowValue(FrameworkInputs.TypeName, type.FullyQualifiedName);
        if (!string.IsNullOrWhiteSpace(assemblyName))
            SetRowValue(FrameworkInputs.AssemblyName, assemblyName);

        NotifyStateChanged();
    }

    private void SetRowValue(string key, string value)
    {
        var row = AdditionalProperties.FirstOrDefault(r => r.Key == key);
        if (row is not null)
            row.Value = value;
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

    public Dictionary<string, string> GetHeaders()
    {
        var headers = new Dictionary<string, string>();
        foreach (var row in HeaderMap)
        {
            if (string.IsNullOrWhiteSpace(row.Key))
                continue;
            headers[row.Key] = row.Value ?? string.Empty;
        }
        return headers;
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