using System.Reflection;
using Hermes;
using Hermes.Blazor;
using Iris.Assemblies;
using Iris.Assemblies.CodeGeneration;
using Iris.Contracts.Assemblies;
using Iris.Brokers;
using Iris.Brokers.Extensions;
using Iris.Brokers.Models;
using Iris.Components;
using Iris.Contracts.Messaging.Frameworks;
using Iris.Desktop.Admin;
using Iris.Desktop.Brokers;
using Iris.Desktop.History;
using Iris.Desktop.Infrastructure;
using Iris.Desktop.NativeMenu;
using Iris.Desktop.PackageManagement;
using Iris.Desktop.Templates;
using Iris.Components.NativeMenu;
using Iris.Desktop.Telemetry;
using Iris.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Iris.Desktop.Configuration;
using Mythetech.Framework.Desktop;
using Mythetech.Framework.Desktop.Hermes;
using Mythetech.Framework.Desktop.Storage.LiteDb;
using Mythetech.Framework.Desktop.Updates;
using Mythetech.Framework.Infrastructure.Guards;
using Mythetech.Framework.Infrastructure.Plugins;
using Mythetech.Framework.Infrastructure.MessageBus;
using Mythetech.Framework.Infrastructure.Initialization;
using Mythetech.Framework.Infrastructure.Settings;
using Velopack;

namespace Iris.Desktop;

public class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        CrashReporter.Install();

        try
        {
            VelopackApp.Build().Run();
        }
        catch (Exception ex)
        {
            CrashReporter.Report("Velopack initialization failed", ex);
        }

        var builder = HermesBlazorAppBuilder.CreateDefault(args);

        // Hermes builds on an empty host, so no logging provider exists unless one is added
        // here. Without this every ILogger call in the application is silently discarded.
        builder.Logging.SetMinimumLevel(builder.Environment.IsDevelopment() ? LogLevel.Debug : LogLevel.Information);
        builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
        builder.Logging.AddSimpleConsole(options =>
        {
            options.TimestampFormat = "HH:mm:ss ";
            options.SingleLine = true;
        });

        builder.RootComponents.Add<App>("#app");

        builder.ConfigureWindow(options =>
        {
            options.Title = "Iris Desktop";
            options.Width = 1920;
            options.Height = 1080;
            options.CenterOnScreen = true;
            options.DevToolsEnabled = true;
            options.CustomTitleBar = true;
        });

        // Iris domain services
        builder.Services.AddIrisComponentServices<LocalConnectionManager, LocalConnectionManager, LocalTemplateService, LocalPackageService, LocalHistoryService, AdminClient, MessageLayoutRepository>();
        builder.Services.AddSingleton<IBrokerConnectionManager, BrokerConnectionManager>();
        builder.Services.AddFrameworkProvider();
        builder.Services.AddScoped<IFrameworkCatalog, LocalFrameworkCatalog>();

        foreach (var implementationType in Assembly.GetAssembly(typeof(IConnector))!.GetTypes()
                     .Where(t => typeof(IConnector).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract))
        {
            builder.Services.AddSingleton(typeof(IConnector), implementationType);
        }

        builder.Services.AddTransient<IAssemblyLoadService, AssemblyLoader>();
        builder.Services.AddTransient<ICodeGenerator, CodeGenerator>();
        builder.Services.AddTransient<ISampleJsonGenerator, SampleJsonGenerator>();
        builder.Services.AddTransient<IrisLiteDbContext>();
        builder.Services.AddTransient<HistoryRepository>();
        builder.Services.AddTransient<ConnectionRepository>();
        builder.Services.AddSingleton<TemplateRepository>();
        builder.Services.AddSingleton<PackageRepository>();
        builder.Services.AddTransient<AutoDiscovery>();
        builder.Services.AddSingleton<ITelemetrySink, MessageBusTelemetrySink>();
        builder.Services.AddSingleton<IOtlpReceiver, OtlpReceiverHost>();

        // Native menu services
        builder.Services.AddSingleton<INativeMenuService, NativeMenuService>();
        builder.Services.AddSingleton<INativeMenuCommandDispatcher, NativeMenuCommandDispatcher>();

        // Framework infrastructure
        builder.Services.AddMessageBus(typeof(Program).Assembly, typeof(IrisServiceRegistrationExtensions).Assembly);
        builder.Services.AddSettingsFramework();
        builder.Services.AddDesktopSettingsStorage("Iris");
        builder.Services.RegisterSettingsFromAssembly(typeof(Program).Assembly);
        builder.Services.RegisterSettingsFromAssembly(typeof(Iris.Components.Messaging.MessagingSettings).Assembly);
        builder.Services.AddDesktopServices(DesktopHost.Hermes);
        builder.Services.RegisterSettingsFromAssembly(typeof(DesktopHost).Assembly);
        builder.Services.AddUpdateService(options =>
        {
            var platform = OperatingSystem.IsWindows() ? "windows"
                : OperatingSystem.IsMacOS() ? "macos"
                : "linux";
            var channel = OperatingSystem.IsWindows() ? "win"
                : OperatingSystem.IsMacOS() ? "osx"
                : "linux";
            options.UpdateUrl = $"{IrisDownloadConfiguration.UpdateBaseUrl}/{platform}";
            options.Channel = channel;
        });
        builder.Services.AddJsGuards();
        builder.Services.AddPluginStateProvider("Iris");
        builder.Services.AddPluginFramework();
        builder.Services.AddAsyncInitialization();
        builder.Services.AddInitializationHook<RestoreConnectionsInitializationHook>();
        builder.Services.AddInitializationHook<AutoDiscoveryInitializationHook>();
        builder.Services.AddInitializationHook<RestorePackagesInitializationHook>();
        builder.Services.AddInitializationHook<SagaTelemetryInitializationHook>();

        try
        {
            var app = builder.Build();

            CrashReporter.AttachLogger(app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Iris.Desktop"));

            app.RegisterHermesProvider();

            var menuService = app.Services.GetRequiredService<INativeMenuService>();
            menuService.Initialize(app.MainWindow.MenuBar);

            app.Services.UseMessageBus(typeof(Program).Assembly, typeof(IrisServiceRegistrationExtensions).Assembly);
            app.Services.UseSettingsFramework();
            app.Services.UseUpdateService();

            app.Run();
        }
        catch (Exception ex)
        {
            // Build, native menu initialization and the window loop all ran outside the
            // previous handler's reach, so a failure in any of them left no record at all.
            CrashReporter.Report("Fatal startup exception", ex);
            throw;
        }
    }
}
