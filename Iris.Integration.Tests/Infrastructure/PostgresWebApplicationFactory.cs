using System;
using Iris.Api.Infrastructure;
using Iris.Api.Services.Integrations;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Iris.Integration.Tests.Infrastructure
{
    public class PostgresWebApplicationFactory : WebApplicationFactory<Program>
    {
        private PostgreSqlContainer? _container;

        public PostgreSqlContainer? Container { get => _container; }

        public async Task<PostgreSqlContainer> CreateAsync()
        {
            return _container = await IrisPostgresDbFactory.Create();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                UsePostgresDbContext(services);

                UseKeyVaultEmulator(services);

                services.AddMassTransitTestHarness();
            });

            builder.UseEnvironment("Integration");
        }

        private void UsePostgresDbContext(IServiceCollection services)
        {
            // Remove the app's DbContext registration.
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType ==
                    typeof(DbContextOptions<IrisCloudDbContext>));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            _container = IrisPostgresDbFactory.Create().GetAwaiter().GetResult();

            services.AddDbContext<IrisCloudDbContext>(options =>
            {
                options.UseNpgsql(_container.GetConnectionString());
            });
        }

        private static void UseKeyVaultEmulator(IServiceCollection services)
        {
            var descriptor = services.SingleOrDefault(
                   d => d.ServiceType ==
                       typeof(ISecretService));

            services.Remove(descriptor);

            services.AddScoped<ISecretService, EmulatedKeyVaultService>();
        }
    }
}

