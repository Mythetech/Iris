using Iris.Contracts.Templates.Models;

namespace Iris.Contracts.Templates.Endpoints
{
    public static class UpdateTemplate
    {
        public record UpdateTemplateRequest(Template Template, bool NewVersion = false);

        public record UpdateTemplateResponse(bool Success);
    }
}

