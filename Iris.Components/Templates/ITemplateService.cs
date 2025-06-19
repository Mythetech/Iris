using System;
using Iris.Contracts.Templates.Models;

namespace Iris.Components.Templates
{
    public interface ITemplateService
    {
        public Task<List<Template>> GetTemplatesAsync();

        public Task<List<VersionedTemplate>> GetTemplatesAndVersionsAsync();

        public Task CreateTemplateAsync(Template template);

        public Task UpdateTemplateAsync(Template template, bool newVersion = false);

        public Task DeleteTemplateAsync(Template template);
    }
}

