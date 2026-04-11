using Iris.Components.Admin;
using Iris.Components.Breadcrumbs;
using Iris.Components.Brokers;
using Iris.Components.History;
using Iris.Components.Theme;
using Mythetech.Framework.Infrastructure.MessageBus;
using Iris.Components.Messaging;
using Iris.Components.PackageManagement;
using Iris.Components.Shared.Time;
using Iris.Components.Templates;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Toolbelt.Blazor.Extensions.DependencyInjection;

namespace Iris.Components
{
    public static class IrisServiceRegistrationExtensions
    {
        public static IServiceCollection AddIrisComponentServices<TBrokerService, TMessageService, TTemplateService, TPackageService, THistoryService, TAdminService, TMessageLayoutService>(
            this IServiceCollection services)
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
            services.AddScoped<IMessageSendOrchestrator, MessageSendOrchestrator>();
            services.AddScoped<IMessagingLayoutService, TMessageLayoutService>();
            services.AddScoped<LayoutState>();
            services.AddScoped<ITemplateService, TTemplateService>();
            services.AddScoped<TemplatesState>();
            services.AddScoped<ITemplatesState>(provider => provider.GetRequiredService<TemplatesState>());
            services.AddScoped<IPackageService, TPackageService>();
            services.AddTransient<IHistoryService, THistoryService>();
            services.AddSingleton<HistoryState>();
            services.AddSingleton<IrisAppState>();
            services.AddScoped<IAdminService, TAdminService>();

            // Dynamic connection data provider lookup (maps normalized provider names to custom connection UI components)
            services.AddSingleton(new Dictionary<string, Type>
            {
                { "rabbitmq", typeof(Brokers.RabbitMqConnectionData) },
                { "amazon", typeof(Brokers.AmazonConnectionData) },
            });

            // Connection details slot registries — defaults are filled in below per broker.
            services.AddSingleton(new Brokers.ConnectionDetails.EndpointsViewRegistry
            {
                { "rabbitmq",        typeof(Brokers.ConnectionDetails.RabbitMqEndpointsView) },
                { "azureservicebus", typeof(Brokers.ConnectionDetails.AzureServiceBusEndpointsView) },
            });
            services.AddSingleton(new Brokers.ConnectionDetails.ReadViewRegistry());
            services.AddSingleton(new Brokers.ConnectionDetails.SendViewRegistry());

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

            return services;
        }
    }
}