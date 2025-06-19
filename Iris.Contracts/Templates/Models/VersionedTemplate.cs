using System;
namespace Iris.Contracts.Templates.Models
{
    public class VersionedTemplate : Template
    {
        public List<TemplateVersion>? VersionHistory { get; set; }
    }
}

