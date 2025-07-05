using System.Net.Http.Headers;
using Iris.Desktop;
using Iris.Integration.Tests.Infrastructure;
using MassTransit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Iris.Integration.Tests
{
    public class IrisWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
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

    }
}
