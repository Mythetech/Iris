using System;
using Iris.Api;
using Iris.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Iris.Integration.Tests.Infrastructure
{
    public static class IrisPostgresDbFactory
    {
        public static async Task<PostgreSqlContainer> Create()
        {
            var container = new PostgreSqlBuilder().WithDatabase("IrisCloudDb").WithUsername("Postgres").WithPassword("Test123!").WithCleanUp(true).Build();
            await container.StartAsync();

            return container;
        }

        public static async Task<IrisCloudDbContext> CreateDbAsync()
        {
            var container = await Create();
            return CreateDb(container);
        }

        public static IrisCloudDbContext CreateDb(PostgreSqlContainer container)
        {

            var connectionString = container.GetConnectionString();

            var builder = new DbContextOptionsBuilder<IrisCloudDbContext>();
            builder.UseNpgsql(connectionString);
            return new IrisCloudDbContext(builder.Options);
        }
    }
}

