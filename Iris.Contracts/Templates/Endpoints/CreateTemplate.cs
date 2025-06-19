using System;
using Iris.Contracts.Templates.Models;

namespace Iris.Contracts.Templates.Endpoints
{
    public static class CreateTemplate
    {
        public record CreateTemplateRequest(Template template);

        public record CreateTemplateResponse(bool Success);
    }
}

