using System.Net.Http.Headers;
using Iris.Api;
using Iris.Api.Infrastructure;
using Iris.Api.Infrastructure.Account;
using Iris.Api.Services.Integrations;
using Iris.Integration.Tests.Infrastructure;
using MassTransit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Iris.Integration.Tests
{
    public class IrisWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                UseTestDbContext(services);

                UseKeyVaultEmulator(services);

                services.AddMassTransitTestHarness();

                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, FakeAuthenticationHandler>("Test", options => { });
            });

            builder.UseEnvironment("Integration");
        }

        protected override void ConfigureClient(HttpClient client)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");
        }

        private static void UseTestDbContext(IServiceCollection services)
        {
            var dbContextDescriptor = services.SingleOrDefault(
                    d => d.ServiceType ==
                        typeof(DbContextOptions<IrisCloudDbContext>));

            services.Remove(dbContextDescriptor);

            services.AddDbContext<IrisCloudDbContext>((container, options) =>
            {
                options.UseInMemoryDatabase("IrisTestDb");
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

