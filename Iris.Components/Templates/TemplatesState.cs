using Iris.Contracts.Templates.Models;
using Microsoft.Extensions.Logging;

namespace Iris.Components.Templates;

public class TemplatesState : ITemplatesState
{
    private readonly ITemplateService _templatesService;
    private readonly ILogger<TemplatesState> _logger;

    private List<Template>? _cachedTemplates;

    public event Action? TemplateStateChanged;

    public event Func<Template, Task>? TemplateLoaded;

    public TemplatesState(ITemplateService templatesService, ILogger<TemplatesState> logger)
    {
        _templatesService = templatesService;
        _logger = logger;
    }

    public List<Template>? Templates => _cachedTemplates;

    /// <summary>
    /// Hands a template to whoever is listening, and waits for them.
    ///
    /// <para>
    /// The subscriber is the messaging editor, which resolves the template's expressions and
    /// writes the result into a Monaco instance, both of which are async. Delivered through a
    /// <see cref="Func{T, TResult}"/> rather than an <see cref="Action"/> so that work is
    /// awaited: an async handler on an Action is async void, which swallows its exceptions
    /// and reports success to the button the user clicked.
    /// </para>
    /// </summary>
    public async Task LoadTemplateAsync(Template template)
    {
        if (TemplateLoaded is null)
            return;

        // One await per subscriber. Invoking a multicast Func returns only the last
        // subscriber's Task, so awaiting the delegate itself would abandon the others.
        foreach (var handler in TemplateLoaded.GetInvocationList().Cast<Func<Template, Task>>())
            await handler(template);
    }

    /// <summary>
    /// The cache, loaded on first use. Every write goes through this first, because three of
    /// the four ways to reach one (the editor page, the rename field on the messaging tab,
    /// and the native menu) can run without anything having listed the templates.
    /// </summary>
    private async Task<List<Template>> LoadedTemplatesAsync() =>
        _cachedTemplates ??= await _templatesService.GetTemplatesAsync();

    public async Task<List<Template>> GetTemplatesAsync()
    {
        if (_cachedTemplates != null)
            return _cachedTemplates;

        var templates = await LoadedTemplatesAsync();

        TemplateStateChanged?.Invoke();

        return templates;
    }

    public async Task CreateTemplateAsync(Template template)
    {
        // Loaded before the write, not after: loading afterwards would read back the row
        // just written and then add it a second time.
        var templates = await LoadedTemplatesAsync();

        await _templatesService.CreateTemplateAsync(template);

        templates.Add(template);

        TemplateStateChanged?.Invoke();
    }

    public async Task UpdateTemplateAsync(Template template, bool newVersion = false)
    {
        var templates = await LoadedTemplatesAsync();

        await _templatesService.UpdateTemplateAsync(template, newVersion);

        int index = templates.FindIndex(x => x.TemplateId.Equals(template.TemplateId));
        if(index >= 0)
            templates[index] = template;

        TemplateStateChanged?.Invoke();
    }

    public async Task DeleteTemplateAsync(Template template)
    {
        var templates = await LoadedTemplatesAsync();

        await _templatesService.DeleteTemplateAsync(template);

        templates.RemoveAll(x => x.TemplateId.Equals(template.TemplateId));

        TemplateStateChanged?.Invoke();
    }

    public async Task<Template> DuplicateTemplateAsync(Template template)
    {
        var duplicate = new Template
        {
            Name = $"{template.Name} (Copy)",
            Json = template.Json,
        };

        await CreateTemplateAsync(duplicate);
        return duplicate;
    }
}

public interface ITemplatesState
{
    /// <summary>
    /// The cached templates, or null before anything has loaded them. Subscribe to
    /// <see cref="TemplateStateChanged"/> to hear when this changes.
    /// </summary>
    List<Template>? Templates { get; }

    /// <summary>
    /// Raised whenever the cached set changes. An event rather than a settable delegate,
    /// because there are four subscribers and any one of them assigning with = would have
    /// silently dropped the other three.
    /// </summary>
    event Action? TemplateStateChanged;

    /// <summary>
    /// Raised by <see cref="LoadTemplateAsync"/>, carrying the template the messaging editor
    /// should open. Returns a Task so the handler's work is awaited rather than abandoned.
    /// </summary>
    event Func<Template, Task>? TemplateLoaded;

    Task LoadTemplateAsync(Template template);

    Task<List<Template>> GetTemplatesAsync();

    Task CreateTemplateAsync(Template template);

    Task UpdateTemplateAsync(Template template, bool newVersion = false);

    Task DeleteTemplateAsync(Template template);

    Task<Template> DuplicateTemplateAsync(Template template);
}
