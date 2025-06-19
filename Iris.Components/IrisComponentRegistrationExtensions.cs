using Iris.Cloud.Client.Identity;
using Iris.Components.Admin;
using Iris.Components.Breadcrumbs;
using Iris.Components.Brokers;
using Iris.Components.History;
using Iris.Components.Identity;
using Iris.Components.Infrastructure.MessageBus;
using Iris.Components.Messaging;
using Iris.Components.PackageManagement;
using Iris.Components.Shared.Time;
using Iris.Components.Subscriptions;
using Iris.Components.Templates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using MudBlazor.Services;
using Toolbelt.Blazor.Extensions.DependencyInjection;

namespace Iris.Components
{
    public static class IrisServiceRegistrationExtensions
    {
        public static IServiceCollection AddIrisComponentServices<TBrokerService, TMessageService, TTemplateService, TPackageService, THistoryService, TAdminService, TMessageLayoutService>(
            this IServiceCollection services,
            Action<IServiceCollection>? configureHttpClients = default)
            where TBrokerService : class, IBrokerService
            where TMessageService : class, IMessageService
            where TTemplateService : class, ITemplateService
            where TPackageService : class, IPackageService
            where THistoryService : class, IHistoryService
            where TAdminService : class, IAdminService
            where TMessageLayoutService : class, IMessagingLayoutService
        {
            // Register broker, messaging, templates, packages, history, and admin services
            services.AddScoped<IBrokerService, TBrokerService>();
            services.AddScoped<IMessageService, TMessageService>();
            services.AddScoped<MessageState>();
            services.AddScoped<IMessagingLayoutService, TMessageLayoutService>();
            services.AddScoped<LayoutState>();
            services.AddScoped<ITemplateService, TTemplateService>();
            services.AddScoped<TemplatesState>();
            services.AddScoped<ITemplatesState>(provider => provider.GetRequiredService<TemplatesState>());
            services.AddScoped<IPackageService, TPackageService>();
            services.AddTransient<IHistoryService, THistoryService>();
            services.AddSingleton<HistoryState>();
            services.AddScoped<IAdminService, TAdminService>();

            // Add HttpClient configurations
            services.AddTransient<CookieHandler>();

            if (configureHttpClients is not null)
            {
                configureHttpClients(services);
            }
            else
            {
                services.AddHttpClient(
                        "Auth",
                        opt => opt.BaseAddress = new Uri(System.Environment.GetEnvironmentVariable("BackendUrl") ?? "https://localhost:5001"))
                    .AddHttpMessageHandler<CookieHandler>();

                services.AddHttpClient(
                        "Iris",
                        opt => opt.BaseAddress = new Uri(System.Environment.GetEnvironmentVariable("BackendUrl") ?? "https://localhost:5001"))
                    .AddHttpMessageHandler<CookieHandler>();
            }

            // Add authorization
            services.AddAuthorizationCore();
            services.AddScoped<AuthenticationStateProvider, CookieAuthenticationStateProvider>();
            services.AddScoped(sp => (IAccountManagement)sp.GetRequiredService<AuthenticationStateProvider>());

            // Add MudBlazor and other UI services
            services.AddMudServices(config =>
            {
                config.SnackbarConfiguration.ShowTransitionDuration = 250;
                config.SnackbarConfiguration.HideTransitionDuration = 250;
                config.SnackbarConfiguration.VisibleStateDuration = 3500;
                config.SnackbarConfiguration.BackgroundBlurred = true;
            });
            services.AddLoadingBarService();
            services.AddBreadcrumbService();
            services.AddViewTransition();
            services.AddBrowserTimeProvider();

            // Add subscription services
            services.AddScoped<SubscriptionStateProvider, SubscriptionService>();
            services.AddScoped<ISubscriptionService, SubscriptionService>();
            services.AddCascadingSubscriptionState();
            
            return services;
        }

        public static IServiceCollection AddIrisConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            // Configure subscription settings
            var subscriptionConfig = new SubscriptionConfiguration
            {
                MonthlyLink = configuration["Subscription:Monthly"] ?? "",
                YearlyLink = configuration["Subscription:Yearly"] ?? ""
            };
            services.AddSingleton(subscriptionConfig);

            // Configure subscription payment link settings
            var paymentConfig = new SubscriptionPaymentLinkSettings();
            configuration.GetSection("Subscription").Bind(paymentConfig);
            services.AddSingleton(paymentConfig);

            // Register provider types
            var providerTypes = new Dictionary<string, Type>
            {
                { "rabbitmq", typeof(RabbitMqConnectionData) },
                { "azure", null! },
                { "amazon", typeof(AmazonConnectionData) }
            };
            services.AddSingleton(providerTypes);

            return services;
        }
        
    }
}