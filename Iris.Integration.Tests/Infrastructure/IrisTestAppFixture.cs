using System;
using System.Net.Http.Headers;
using Bogus.DataSets;
using FastEndpoints.Testing;
using FluentAssertions.Common;
using Iris.Api.Infrastructure;
using Iris.Api.Infrastructure.Account;
using Iris.Api.Services.Integrations;
using Iris.Api.Services.Subscriptions;
using Iris.Api.Services.Subscriptions.Domain;
using Iris.Api.Services.User;
using Iris.Brokers;
using Iris.Brokers.Models;
using Iris.Integration.Tests.Brokers.EmulatedProvider;
using MassTransit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Iris.Integration.Tests.Infrastructure
{
    [DisableWafCache]
    public class IrisTestAppFixture : AppFixture<Program>
    {
        protected override async Task SetupAsync()
        {
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");
            
            using var s = this.Services.CreateScope();
            
            //Seed authenticated test user
            var userManager = s.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

            var r = await userManager.CreateAsync(new AppUser()
            {
                Email = "test@test.com",
                UserName = "Test",
                TenantId = Guid.Empty,
                UserId = Guid.Empty,
            });
            
            //Seed subscription for test user
            var subService = s.ServiceProvider.GetRequiredService<ISubscriptionWriteService>();
            await subService.UpsertSubscriptionAsync("Active", "test@test.com");

            //Seed emulated broker as default connection
            var cm = s.ServiceProvider.GetRequiredService<IBrokerConnectionManager>();
            var connector = cm.GetProviders().First(x => x.Provider.Equals("Mock"));

            var connection = await connector.ConnectAsync(new ConnectionData()
            {
                Uri = "fakeaddress"
            });
            
            await cm.AddConnectionAsync(connection);
        }

        protected override void ConfigureApp(IWebHostBuilder a)
        {
            a.UseEnvironment("Integration");
        }

        protected override void ConfigureServices(IServiceCollection s)
        {
            UseTestDbContext(s);

            UseKeyVaultEmulator(s);

            s.AddMassTransitTestHarness();

            s.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, FakeAuthenticationHandler>("Test", options => { });

            s.AddSingleton<IConnector, EmulatedProvider>();

            s.AddScoped<IAccountContext, AccountContext>();
        }

        protected override Task TearDownAsync()
        {
            return Task.CompletedTask;
        }

        private static void UseTestDbContext(IServiceCollection services)
        {
             var dbContextDescriptor = services.SingleOrDefault(
                    d => d.ServiceType ==
                        typeof(DbContextOptions<IrisCloudDbContext>));

            if (dbContextDescriptor != null)
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

            if (descriptor != null)
                services.Remove(descriptor);

            services.AddScoped<ISecretService, EmulatedKeyVaultService>();
        }
    }
}

