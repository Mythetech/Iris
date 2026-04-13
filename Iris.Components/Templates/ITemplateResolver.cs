namespace Iris.Components.Templates;

public interface ITemplateResolver
{
    Task<string?> ResolveAsync(string? templateJson);
}
