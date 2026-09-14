using Iris.Components.Admin;
using Iris.Components.Breadcrumbs;
using Iris.Components.Brokers;
using Iris.Components.CommandPalette;
using Iris.Components.History;
using Iris.Components.Infrastructure;
using Iris.Components.Theme;
using Mythetech.Framework.Infrastructure.MessageBus;
using Iris.Components.Messaging;
using Iris.Components.PackageManagement;
using Iris.Components.Sagas;
using Iris.Components.Shared.Time;
using Iris.Components.Templates;
using Iris.Sagas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
            // Register broker, messaging, templates, packages, history, and admin services.
            //
            // The two interfaces are mapped onto the concrete registrations rather than
            // registered separately, the same way ITemplatesState is below. Both are
            // LocalConnectionManager on the desktop host, and two AddScoped calls built it
            // twice per scope, each copy taking its own ConnectionRepository and, through
            // it, its own database handle. Open connections survived that because they
            // live in the singleton IBrokerConnectionManager, which is why it went
            // unnoticed. A host that supplies two different types still gets two.
            services.TryAddScoped<TBrokerService>();
            services.TryAddScoped<TMessageService>();
            services.AddScoped<IBrokerService>(provider => provider.GetRequiredService<TBrokerService>());
            services.AddScoped<IMessageService>(provider => provider.GetRequiredService<TMessageService>());
            services.AddScoped<MessageState>();
            services.AddScoped<IMessageSendOrchestrator, MessageSendOrchestrator>();
            services.AddScoped<IMessagingLayoutService, TMessageLayoutService>();
            services.AddScoped<LayoutState>();
            services.AddSingleton<ITemplateService, TTemplateService>();
            services.AddScoped<TemplatesState>();
            services.AddScoped<ITemplatesState>(provider => provider.GetRequiredService<TemplatesState>());
            services.AddScoped<ITemplateResolver, TemplateResolver>();
            services.AddSingleton<IPackageService, TPackageService>();
            services.AddTransient<IHistoryService, THistoryService>();
            services.AddSingleton<HistoryState>();
            services.AddSingleton<IrisAppState>();
            services.AddSagaServices();
            services.AddSingleton<SagaDefinitionState>();
            services.AddSingleton<SagaInstanceState>();
            services.AddSingleton<ReceivedSpanLog>();
            services.AddScoped<IAdminService, TAdminService>();

            // Per-broker UI slots. A broker with no entry falls back to the default view,
            // so adding one needs no change to the components that dispatch on these.
            services.AddSingleton(new ComponentRegistry<Brokers.IConnectionDataProvider>()
                .Register<Brokers.RabbitMqConnectionData>("RabbitMq")
                .Register<Brokers.AmazonConnectionData>("Amazon"));

            services.AddSingleton(new ComponentRegistry<Brokers.ConnectionDetails.IConnectionEndpointsView>()
                .Register<Brokers.ConnectionDetails.RabbitMqEndpointsView>("RabbitMq")
                .Register<Brokers.ConnectionDetails.AzureServiceBusEndpointsView>("AzureServiceBus"));

            services.AddSingleton(new ComponentRegistry<Brokers.ConnectionDetails.IConnectionReadView>());
            services.AddSingleton(new ComponentRegistry<Brokers.ConnectionDetails.IConnectionSendView>());

            // Add MudBlazor and other UI services
            services.AddMudServices(config =>
            {
                config.SnackbarConfiguration.ShowTransitionDuration = 250;
                config.SnackbarConfiguration.HideTransitionDuration = 250;
                config.SnackbarConfiguration.VisibleStateDuration = 3500;
                config.SnackbarConfiguration.BackgroundBlurred = true;
                config.PopoverOptions.OverflowBehavior = MudBlazor.OverflowBehavior.FlipNever;
            });
            services.AddLoadingBarService();
            services.AddBreadcrumbService();
            services.AddViewTransition();
            services.AddBrowserTimeProvider();
            services.AddCommandPalette();

            return services;
        }
    }
}