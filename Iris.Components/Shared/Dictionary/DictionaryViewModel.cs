namespace Iris.Components.Messaging
{
    public class DictionaryViewModel
    {

        public DictionaryViewModel() { }

        public DictionaryViewModel(string? key, string? value)
        {
            Key = key;
            Value = value;
        }

        public string? Key { get; set; }

        public string? Value { get; set; }

        public string? Description { get; set; }

        /// <summary>
        /// Indicates the record should not be able to be removed or edited from the UI
        /// </summary>
        public bool Immutable { get; set; }

        /// <summary>When set, the value cell renders as a select limited to these values.</summary>
        public IReadOnlyList<string>? AllowedValues { get; set; }

        /// <summary>A framework input the send cannot proceed without.</summary>
        public bool Required { get; set; }
    }
}
