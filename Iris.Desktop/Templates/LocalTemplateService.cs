using Iris.Components.Templates;
using Iris.Contracts.Templates.Models;
using System.Collections.Concurrent;

namespace Iris.Desktop.Templates;

public class LocalTemplateService : ITemplateService
{
    private readonly ConcurrentDictionary<Guid, Template> _templates = new();
    private readonly ConcurrentDictionary<Guid, List<VersionedTemplate>> _versionedTemplates = new();
    private readonly TemplateRepository _repository;

    public LocalTemplateService(TemplateRepository repository)
    {
        _repository = repository;

        foreach (var persisted in _repository.GetAll())
        {
            var template = persisted.ToTemplate();
            _templates[template.TemplateId] = template;
        }
    }

    public Task<List<Template>> GetTemplatesAsync()
    {
        var templates = _templates.Values.ToList();
        return Task.FromResult(templates);
    }

    public Task<List<VersionedTemplate>> GetTemplatesAndVersionsAsync()
    {
        var versionedList = _versionedTemplates.Values.SelectMany(v => v).ToList();
        return Task.FromResult(versionedList);
    }

    public Task CreateTemplateAsync(Template template)
    {
        if (template == null)
        {
            throw new ArgumentException("Template is invalid");
        }

        if (template.TemplateId == Guid.Empty)
        {
            template = new Template()
            {
                TemplateId = Guid.CreateVersion7(),
                Json = template.Json,
                Name = template.Name,
                Version = template.Version,
            };
        }

        if (!_templates.TryAdd(template.TemplateId, template))
        {
            throw new InvalidOperationException("A template with the same ID already exists.");
        }

        _versionedTemplates[template.TemplateId] = new List<VersionedTemplate>
        {
            new VersionedTemplate { TemplateId = template.TemplateId, Version = 1, Json = template.Json, Name = template.Name }
        };

        _repository.Save(PersistentTemplate.FromTemplate(template));

        return Task.CompletedTask;
    }

    public Task UpdateTemplateAsync(Template template, bool newVersion = false)
    {
        if (template == null || template.TemplateId == Guid.Empty)
        {
            throw new ArgumentException("Template is invalid or missing an ID.");
        }

        if (!_templates.ContainsKey(template.TemplateId))
        {
            throw new KeyNotFoundException("Template not found.");
        }

        _templates[template.TemplateId] = template;

        if (newVersion)
        {
            var versionList = _versionedTemplates.GetOrAdd(template.TemplateId, _ => new List<VersionedTemplate>());
            var latestVersion = versionList.Max(v => v.Version);
            versionList.Add(new VersionedTemplate { TemplateId = template.TemplateId, Version = latestVersion + 1, Json = template.Json, Name = template.Name });
        }

        _repository.Save(PersistentTemplate.FromTemplate(template));

        return Task.CompletedTask;
    }

    public Task DeleteTemplateAsync(Template template)
    {
        if (template == null || template.TemplateId == Guid.Empty)
        {
            throw new ArgumentException("Template is invalid or missing an ID.");
        }

        _templates.TryRemove(template.TemplateId, out _);
        _versionedTemplates.TryRemove(template.TemplateId, out _);

        _repository.Delete(template.TemplateId);

        return Task.CompletedTask;
    }
}
