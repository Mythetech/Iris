using System;
namespace Iris.Contracts.Templates.Endpoints
{
    public static class GetTemplates
    {
        public record GetTemplatesResponse(List<Models.Template> Templates);
    }
}

