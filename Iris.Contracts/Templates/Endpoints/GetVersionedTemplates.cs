namespace Iris.Contracts.Templates.Endpoints
{
    public static class GetVersionedTemplates
    {
        public record GetVersionedTemplatesResponse(List<Models.VersionedTemplate> Templates);
    }
}

