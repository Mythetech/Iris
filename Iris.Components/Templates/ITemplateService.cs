using System;
using Iris.Contracts.Templates.Models;

namespace Iris.Components.Templates
{
    public interface ITemplateService
    {
        public Task<List<Template>> GetTemplatesAsync();

        public Task CreateTemplateAsync(Template template);

        public Task UpdateTemplateAsync(Template template);

        public Task DeleteTemplateAsync(Template template);
    }
}

