using System.Text.Json.Nodes;
using FluentAssertions;
using Iris.Api.Infrastructure;
using Iris.Api.Infrastructure.Account;
using Iris.Api.Services.Templates;
using Iris.Contracts.Templates.Models;
using Domain = Iris.Api.Services.Templates.Domain;
using Iris.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;

namespace Iris.Integration.Tests.Templates
{
    public class TemplateServiceTests : IClassFixture<IrisWebApplicationFactory>, IAsyncLifetime
    {
        private PostgreSqlContainer _container = default!;
        private IrisCloudDbContext _db = default!;
        private readonly IrisWebApplicationFactory _factory;
        private IAccountContext _context;
        private ILogger<TemplateService> _logger;

        private TemplateService _service;

        public TemplateServiceTests(IrisWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact(DisplayName = "Can add a template successfully")]
        public async Task Can_AddTemplate_Successfully()
        {
            // Arrange
            var template = new Template()
            {
                Name = "TestTemplate",
                Json = @"{
  ""number"": 42,
  ""text"": ""Hello, world""
}"
            };

            // Act
            await _service.SaveTemplateAsync(template);

            // Assert
            var db = IrisPostgresDbFactory.CreateDb(_container);
            var t = await db.Templates.FirstOrDefaultAsync(x => x.Name.Equals(template.Name));

            db.Templates.Count().Should().BeGreaterThan(0);
            t?.Should().NotBeNull();
            t?.Id.Should().BeGreaterThan(0);
            t?.TenantId.Should().Be(_context.TenantId);
            t?.UserId.Should().Be(_context.UserId);
            var j = JsonObject.Parse(t.Json);
            j["number"]?.GetValue<int>().Should().Be(42);
            j["text"]?.GetValue<string>().Should().Be("Hello, world");
        }

        [Fact(DisplayName = "Retrieves templates from database")]
        public async Task GetTemplatesAsync_ReturnsTemplates()
        {
            // Arrange
            var expectedTemplates = new List<Domain.Template>
    {
        new Domain.Template { Name = "Template1", Json = "{}", Version = 1, TenantId = _context.TenantId, UserId = _context.UserId },
        new Domain.Template { Name = "Template2", Json = "{}", Version = 2, TenantId = _context.TenantId, UserId = _context.UserId },
    };
            var db = IrisPostgresDbFactory.CreateDb(_container);

            await db.Templates.AddRangeAsync(expectedTemplates);
            await db.SaveChangesAsync();

            // Act
            var actualTemplates = await _service.GetTemplatesAsync();

            // Assert
            actualTemplates.Should().NotBeNull();
            actualTemplates.Count.Should().Be(expectedTemplates.Count);
            for (int i = 0; i < expectedTemplates.Count; i++)
            {
                actualTemplates[i].Name.Should().Be(expectedTemplates[i].Name);
                actualTemplates[i].Json.Should().Be(expectedTemplates[i].Json);
                actualTemplates[i].Version.Should().Be(expectedTemplates[i].Version);
            }
        }

        [Fact(DisplayName = "Returns versioned templates from database")]
        public async Task GetTemplatesAndVersionsAsync_ReturnsTemplatesWithVersions()
        {
            // Arrange
            var user1 = Guid.NewGuid();
            var user2 = Guid.NewGuid();

            var expectedTemplates = new List<Domain.Template>
    {
        new Domain.Template
        {
            Name = "Template1",
            Json = "{}",
            Version = 1,
            VersionHistory = new List<Domain.TemplateVersion>
            {
                new Domain.TemplateVersion { Version = 1, Json = "{}", ModifiedBy = user1, ModifiedDate = DateTimeOffset.UtcNow, ModifiedUser = "User1" }
            },
            TenantId = _context.TenantId,
            UserId = _context.UserId
        },
        new Domain.Template
        {
            Name = "Template2",
            Json = "{}",
            Version = 2,
            VersionHistory = new List<Domain.TemplateVersion>
            {
                new Domain.TemplateVersion { Version = 1, Json = "{}", ModifiedBy = user2, ModifiedDate = DateTimeOffset.UtcNow, ModifiedUser = "User2" },
                new Domain.TemplateVersion { Version = 2, Json = "{}", ModifiedBy = user2, ModifiedDate = DateTimeOffset.UtcNow, ModifiedUser = "User2" }
            },
            TenantId = _context.TenantId,
            UserId = _context.UserId
        },
    };
            await _db.Templates.AddRangeAsync(expectedTemplates);
            await _db.SaveChangesAsync();

            // Act
            var actualTemplates = await _service.GetTemplatesAndVersionsAsync();

            // Assert
            actualTemplates.Should().NotBeNull();
            actualTemplates.Count().Should().Be(expectedTemplates.Count);
            for (int i = 0; i < expectedTemplates.Count; i++)
            {
                actualTemplates[i].Name.Should().Be(expectedTemplates[i].Name);
                actualTemplates[i].Json.Should().Be(expectedTemplates[i].Json);
                actualTemplates[i].Version.Should().Be(expectedTemplates[i].Version);
                actualTemplates[i].VersionHistory.Count.Should().Be(expectedTemplates[i].VersionHistory.Count);
                for (int j = 0; j < expectedTemplates[i].VersionHistory.Count; j++)
                {
                    actualTemplates[i].VersionHistory[j].Version.Should().Be(expectedTemplates[i].VersionHistory[j].Version);
                    actualTemplates[i].VersionHistory[j].Json.Should().Be(expectedTemplates[i].VersionHistory[j].Json);
                    actualTemplates[i].VersionHistory[j].ModifiedBy.Should().Be(expectedTemplates[i].VersionHistory[j].ModifiedBy);
                    actualTemplates[i].VersionHistory[j].ModifiedDate.Date.Should().Be(expectedTemplates[i].VersionHistory[j].ModifiedDate.Date);
                    actualTemplates[i].VersionHistory[j].ModifiedUser.Should().Be(expectedTemplates[i].VersionHistory[j].ModifiedUser);
                }
            }
        }

        [Fact(DisplayName = "Can delete a template successfully")]
        public async Task Can_DeleteTemplate_Successfully()
        {
            // Arrange
            var template = new Template()
            {
                Name = "TestTemplateToDelete",
                Json = @"{
  ""number"": 42,
  ""text"": ""Hello, world""
}"
            };

            await _service.SaveTemplateAsync(template);
            var db = IrisPostgresDbFactory.CreateDb(_container);
            var t = await db.Templates.FirstOrDefaultAsync(x => x.Name.Equals(template.Name));
            t.Should().NotBeNull("Template should have been created successfully");

            // Act
            await _service.DeleteTemplateAsync(t.TemplateKey);

            // Assert
            var verifyDb = IrisPostgresDbFactory.CreateDb(_container);
            var deletedTemplate = await verifyDb.Templates.FindAsync(t.Id);
            deletedTemplate.Should().BeNull("Template should have been deleted successfully");
        }

        [Fact(DisplayName = "Can update a template and create a new version successfully")]
        public async Task Can_UpdateTemplateAndCreateNewVersion_Successfully()
        {
            // Arrange
            var templateId = Guid.NewGuid();

            var template = new Template()
            {
                TemplateId = templateId,
                Name = "TestTemplateToUpdate",
                Json = @"{
            ""number"": 42,
            ""text"": ""Hello, world""
        }"
            };

            await _service.SaveTemplateAsync(template);
            var db = IrisPostgresDbFactory.CreateDb(_container);
            var t = await db.Templates.FirstOrDefaultAsync(x => x.TemplateKey == templateId);
            t.Should().NotBeNull("Template should have been created successfully");

            // Act
            var updatedTemplate = new Template()
            {
                TemplateId = templateId,
                Name = "UpdatedTestTemplate",
                Json = @"{
            ""number"": 43,
            ""text"": ""Hello, universe""
        }"
            };
            var result = await _service.UpdateTemplateAsync(updatedTemplate, true);

            // Assert
            result.Should().BeTrue("Template should have been updated successfully");
            var verifyDb = IrisPostgresDbFactory.CreateDb(_container);
            var updatedT = await verifyDb.Templates.Include(t => t.VersionHistory).FirstOrDefaultAsync(x => x.TemplateKey == templateId);
            updatedT.Should().NotBeNull("Updated template should exist");
            updatedT.Name.Should().Be(updatedTemplate.Name);
            updatedT.Version.Should().Be(2);
            updatedT.VersionHistory.Count.Should().Be(2);
            var j = JsonObject.Parse(updatedT.Json);
            j["number"]?.GetValue<int>().Should().Be(43);
            j["text"]?.GetValue<string>().Should().Be("Hello, universe");
        }

        public Task DisposeAsync()
        {
            return _container.DisposeAsync().AsTask();
        }

        public async Task InitializeAsync()
        {
            _container = await IrisPostgresDbFactory.Create();
            _db = IrisPostgresDbFactory.CreateDb(_container);

            await _db.Database.MigrateAsync();

            var logger = _logger = _factory.Services.GetRequiredService<ILogger<TemplateService>>();
            var context = _context = _factory.Services.GetRequiredService<IAccountContext>();

            _context.TenantId = Guid.NewGuid();
            _context.UserId = Guid.NewGuid();

            _service = new TemplateService(_db, context, logger);
        }
    }
}

