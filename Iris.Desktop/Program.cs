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
using Iris.Desktop.Admin;
using Iris.Desktop.Brokers;
using Iris.Desktop.History;
using Iris.Desktop.Infrastructure;
using Iris.Desktop.NativeMenu;
using Iris.Desktop.PackageManagement;
using Iris.Desktop.Templates;
using Iris.Components.NativeMenu;
using Microsoft.Extensions.DependencyInjection;
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
        try
        {
            VelopackApp.Build().Run();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Velopack initialization failed: {ex.Message}");
        }

        var builder = HermesBlazorAppBuilder.CreateDefault(args)
            .WithLicenseKey(HermesLicense.Key);

        builder.Services.AddLogging();
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

        // Native menu services
        builder.Services.AddSingleton<INativeMenuService, NativeMenuService>();
        builder.Services.AddSingleton<INativeMenuCommandDispatcher, NativeMenuCommandDispatcher>();

        // Framework infrastructure
        builder.Services.AddMessageBus(typeof(Program).Assembly);
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

        var app = builder.Build();

        app.RegisterHermesProvider();

        // Initialize native menus
        var menuService = app.Services.GetRequiredService<INativeMenuService>();
        menuService.Initialize(app.MainWindow.MenuBar);

        app.Services.UseMessageBus(typeof(Program).Assembly);
        app.Services.UseSettingsFramework();
        app.Services.UseUpdateService();
        AppDomain.CurrentDomain.UnhandledException += (sender, error) =>
        {
            Console.Error.WriteLine($"Fatal exception: {error.ExceptionObject}");
        };

        app.Run();
    }
}
