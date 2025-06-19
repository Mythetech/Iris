using System;
namespace Iris.Contracts.Templates.Models
{
    public class TemplateVersion
    {
        /// <summary>
        /// Version of the template
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// The template json associated with this version
        /// </summary>
        public required string Json { get; set; }

        /// <summary>
        /// The date it was modified
        /// </summary>
        public DateTimeOffset ModifiedDate { get; set; }

        /// <summary>
        /// The user id of who modified the version
        /// </summary>
        public Guid ModifiedBy { get; set; }

        /// <summary>
        /// Friendly name of who modified the version
        /// </summary>
        public string ModifiedUser { get; set; } = "";
    }
}

