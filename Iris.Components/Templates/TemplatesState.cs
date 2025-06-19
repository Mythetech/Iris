using Iris.Contracts.Templates.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Iris.Components.Templates;

public class TemplatesState : ITemplatesState
{
    private readonly ITemplateService _templatesService;
    private readonly ILogger<TemplatesState> _logger;

    private List<Template>? _cachedTemplates;

    public Action TemplateStateChanged { get; set; } = default!;
    
    public Action<Template> TemplateLoaded { get; set; } = default!;

    public TemplatesState(ITemplateService templatesService, ILogger<TemplatesState> logger)
    {
        _templatesService = templatesService;
        _logger = logger;
    }
    
    public List<Template>? Templates => _cachedTemplates;

    public void LoadTemplate(Template template)
    {
        TemplateLoaded?.Invoke(template);
    }
    
    public async Task<List<Template>> GetTemplatesAsync()
    {
        if(_cachedTemplates != null)
            return _cachedTemplates;
        
        _cachedTemplates = await _templatesService.GetTemplatesAsync();
        
        TemplateStateChanged?.Invoke();
        
        return _cachedTemplates;
    }   

    public async Task CreateTemplateAsync(Template template)
    {
        await _templatesService.CreateTemplateAsync(template);

        _cachedTemplates ??= new();
        
        _cachedTemplates.Add(template);
        
        TemplateStateChanged?.Invoke();
    }

    public async Task UpdateTemplateAsync(Template template, bool newVersion = false)
    {
        await _templatesService.UpdateTemplateAsync(template, newVersion);

        int index = _cachedTemplates.FindIndex(x => x.TemplateId.Equals(template.TemplateId));
        if(index >= 0)
            _cachedTemplates[index] = template;

        TemplateStateChanged?.Invoke();
    }

    public async Task DeleteTemplateAsync(Template template)
    {
        await _templatesService.DeleteTemplateAsync(template);
        
        _cachedTemplates.RemoveAll(x => x.TemplateId.Equals(template.TemplateId));
        
        TemplateStateChanged?.Invoke();
    }
}

public interface ITemplatesState
{
    public Task<List<Template>> GetTemplatesAsync();

    public Task CreateTemplateAsync(Template template);

    public Task UpdateTemplateAsync(Template template, bool newVersion = false);
    
    public Task DeleteTemplateAsync(Template template);
}
