using Iris.Components.Messaging;
using Iris.Contracts.Brokers.Models;

public class MessageState
{
    private const string IrisHeaderKey = "iris-key";

    private const string DefaultIrisKeyMessage = "{{ Generated on Send }}";

    private static DictionaryViewModel IrisKey => new DictionaryViewModel()
    {
        Key = IrisHeaderKey,
        Value = DefaultIrisKeyMessage,
        Immutable = true
    };
    
    public int MaxDelay { get; set; } = 60;
    public bool Sending { get; set; }
    public int Delay { get; set; }
    public string DelayText { get; set; } = string.Empty;
    public int Repeat { get; set; }
    public string RepeatText { get; set; } = string.Empty;

    public string? SelectedFramework { get; private set; }

    public bool SendIrisHeader { get; private set; } = true;
    
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

    public List<DictionaryViewModel> HeaderMap { get; set; } = new()
    {
        IrisKey
    };

    public void NotifyStateChanged()
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

    public void EnableIrisHeader()
    {
        SendIrisHeader = true;
        if (!HeaderMap.Any(x => x.Key.Equals(IrisHeaderKey)))
        {
            HeaderMap.Add(IrisKey);
        }
        
        StateChanged?.Invoke();
    }
    
    public void DisableIrisHeader()
    {
        SendIrisHeader = false;
        HeaderMap.RemoveAll(x => x.Key != null && x.Key.Contains("iris-"));
        StateChanged?.Invoke();
    }
}