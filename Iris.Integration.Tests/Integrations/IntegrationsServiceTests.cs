using System.Text.Json;
using FluentAssertions;
using Iris.Api.Infrastructure;
using Iris.Api.Infrastructure.Account;
using Iris.Api.Services.Integrations;
using Iris.Api.Services.Integrations.Domain;
using Iris.Contracts.Brokers.Models;
using Iris.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Iris.Integration.Tests.Integrations
{
    public class IntegrationsServiceTests : IClassFixture<PostgresWebApplicationFactory>, IAsyncLifetime
    {
        private IrisCloudDbContext _db = default!;
        private readonly PostgresWebApplicationFactory _factory;
        private PostgreSqlContainer _container = default!;
        private IAccountContext? _context;

        public IntegrationsServiceTests(PostgresWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact(DisplayName = "Returns empty list of integrations if not found")]
        public async Task Retrieves_EmptyList_WhenNoRecords()
        {
            using var scope = _factory.Services.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IIntegrationsService>();

            var integrations = await service.GetIntegrationsAsync();

            integrations.Should().BeEmpty();
        }

        [Fact(DisplayName = "Can retrieve integrations for account")]
        public async Task GetIntegrationsAsync_ShouldReturnIntegrations()
        {
            using var scope = _factory.Services.CreateAsyncScope();

            var context = scope.ServiceProvider.GetRequiredService<IAccountContext>();
            context.UserId = _context.UserId;
            context.TenantId = _context.TenantId;

            // Arrange
            var integration1 = new Api.Services.Integrations.Domain.Integration
            {
                TenantId = _context.TenantId,
                UserId = _context.UserId,
                Address = "http://example.com",
                Provider = "Provider1",
                ConnectionKey = JsonSerializer.Serialize(new ConnectionData())
            };

            var integration2 = new Api.Services.Integrations.Domain.Integration
            {
                TenantId = _context.TenantId,
                UserId = _context.UserId,
                Address = "http://example2.com",
                Provider = "Provider2",
                ConnectionKey = JsonSerializer.Serialize(new ConnectionData())
            };

            var db = scope.ServiceProvider.GetRequiredService<IrisCloudDbContext>();

            await db.Integrations.AddRangeAsync(integration1, integration2);
            await db.SaveChangesAsync();

            // Act
            var service = scope.ServiceProvider.GetRequiredService<IIntegrationsService>();
            var result = await service.GetIntegrationsAsync();

            // Assert
            result.Should().HaveCount(2);
            result[0].Address.Should().Be(integration1.Address);
            result[0].Provider.Should().Be(integration1.Provider);
            result[1].Address.Should().Be(integration2.Address);
            result[1].Provider.Should().Be(integration2.Provider);
        }

        [Fact(DisplayName = "Can not retrieve integrations for other tenants")]
        public async Task GetIntegrationsAsync_ShouldNotReturnIntegrations_AcrossBoundary()
        {
            using var scope = _factory.Services.CreateAsyncScope();

            var context = scope.ServiceProvider.GetRequiredService<IAccountContext>();
            context.UserId = Guid.NewGuid();
            context.TenantId = Guid.NewGuid();

            // Arrange
            var integration1 = new Api.Services.Integrations.Domain.Integration
            {
                TenantId = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                Address = "http://example.com",
                Provider = "Provider1",
                ConnectionKey = JsonSerializer.Serialize(new ConnectionData())
            };

            var db = scope.ServiceProvider.GetRequiredService<IrisCloudDbContext>();

            await db.Integrations.AddRangeAsync(integration1);
            await db.SaveChangesAsync();

            // Act
            var service = scope.ServiceProvider.GetRequiredService<IIntegrationsService>();
            var result = await service.GetIntegrationsAsync();

            // Assert
            result.Should().BeEmpty();
        }

        [Fact(DisplayName = "Returns correctly saved connection data when retrieved from a secret")]
        public async Task GetIntegrationsAsync_ShouldNotReturnFreshConnectionData_WhenOneIsFound()
        {
            using var scope = _factory.Services.CreateAsyncScope();

            var context = scope.ServiceProvider.GetRequiredService<IAccountContext>();
            context.UserId = _context.UserId;
            context.TenantId = _context.TenantId;

            // Arrange
            var connectionData = new ConnectionData
            {
                ConnectionString = "TestConnectionString",
            };

            var secretService = scope.ServiceProvider.GetRequiredService<ISecretService>();

            var secretKey = await secretService.CreateSecretAsync(JsonSerializer.Serialize(connectionData));

            var integration = new Api.Services.Integrations.Domain.Integration
            {
                TenantId = _context.TenantId,
                UserId = _context.UserId,
                Address = "http://example.com",
                Provider = "Provider1",
                SecretKey = secretKey,
                ConnectionKey = "",
            };

            var db = scope.ServiceProvider.GetRequiredService<IrisCloudDbContext>();

            await db.Integrations.AddAsync(integration);
            await db.SaveChangesAsync();


            // Act
            var service = scope.ServiceProvider.GetRequiredService<IIntegrationsService>();
            var result = await service.GetIntegrationsAsync();

            // Assert
            result.Should().HaveCount(1);
            var returnedConnectionData = result[0].Data;
            returnedConnectionData.ConnectionString.Should().BeEquivalentTo(connectionData.ConnectionString);
        }

        public Task DisposeAsync()
        {
            return _container.DisposeAsync().AsTask();
        }

        public async Task InitializeAsync()
        {

            _container = await _factory.CreateAsync();

            using var scope = _factory.Services.CreateAsyncScope();
            _context = scope.ServiceProvider.GetRequiredService<IAccountContext>();
            _context.TenantId = Guid.NewGuid();
            _context.UserId = Guid.NewGuid();
            _db = scope.ServiceProvider.GetRequiredService<IrisCloudDbContext>();
            await _db.Database.MigrateAsync();
        }
    }
}

