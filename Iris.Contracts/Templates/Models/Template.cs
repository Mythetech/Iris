using System;
using System.Text.Json;

namespace Iris.Contracts.Templates.Models
{
    public class Template
    {
        public Guid TemplateId { get; init; }

        public string Name { get; set; } = "";

        public string Json { get; set; } =
        @"{

        }";

        public int Version { get; set; }

        private string? _formattedJson;
        private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

        public string FormattedJson
        {
            get
            {
                if (_formattedJson == null)
                {
                    try
                    {
                        var jsonDocument = JsonDocument.Parse(Json);
                        _formattedJson = JsonSerializer.Serialize(jsonDocument.RootElement, _options);
                    }
                    catch (JsonException)
                    {
                        _formattedJson = Json;
                    }
                }

                return _formattedJson;
            }
        }
    }
}

