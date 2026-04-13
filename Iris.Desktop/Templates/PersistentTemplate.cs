using Iris.Contracts.Templates.Models;
using Iris.Desktop.Infrastructure;

namespace Iris.Desktop.Templates;

public class PersistentTemplate : ILocalEntity
{
    public int Id { get; set; }

    public Guid TemplateId { get; set; }

    public string Name { get; set; } = "";

    public string Json { get; set; } = "";

    public int Version { get; set; }

    public Template ToTemplate()
    {
        return new Template
        {
            TemplateId = TemplateId,
            Name = Name,
            Json = Json,
            Version = Version
        };
    }

    public static PersistentTemplate FromTemplate(Template template)
    {
        return new PersistentTemplate
        {
            TemplateId = template.TemplateId,
            Name = template.Name,
            Json = template.Json,
            Version = template.Version
        };
    }
}
