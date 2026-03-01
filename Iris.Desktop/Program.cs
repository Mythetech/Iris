using System.Reflection;
using Hermes;
using Hermes.Blazor;
using Iris.Assemblies;
using Iris.Assemblies.CodeGeneration;
using Iris.Brokers;
using Iris.Brokers.Extensions;
using Iris.Brokers.Models;
using Iris.Components;
using Iris.Desktop.Admin;
using Iris.Desktop.Brokers;
using Iris.Desktop.History;
using Iris.Desktop.Infrastructure;
using Iris.Desktop.PackageManagement;
using Iris.Desktop.Templates;
using Microsoft.Extensions.DependencyInjection;
using Mythetech.Framework.Desktop;
using Mythetech.Framework.Infrastructure.MessageBus;
using Mythetech.Framework.Infrastructure.Settings;

namespace Iris.Desktop;

public class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var builder = HermesBlazorAppBuilder.CreateDefault(args);

        builder.Services.AddLogging();
        builder.RootComponents.Add<App>("#app");

        builder.ConfigureWindow(options =>
        {
            options.Title = "Iris Desktop";
            options.Width = 1920;
            options.Height = 1080;
            options.CenterOnScreen = true;
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
        builder.Services.AddTransient<IrisLiteDbContext>();
        builder.Services.AddTransient<HistoryRepository>();
        builder.Services.AddTransient<AutoDiscovery>();

        // Framework infrastructure
        builder.Services.AddMessageBus(typeof(Program).Assembly);
        builder.Services.AddSettingsFramework();
        builder.Services.AddDesktopSettingsStorage("Iris");
        builder.Services.RegisterSettingsFromAssembly(typeof(Program).Assembly);
        builder.Services.RegisterSettingsFromAssembly(typeof(Iris.Components.Messaging.MessagingSettings).Assembly);
        builder.Services.AddDesktopServices(DesktopHost.Hermes);

        var app = builder.Build();

        app.Services.UseMessageBus(typeof(Program).Assembly);
        app.Services.UseSettingsFramework();
        app.Services.LoadPersistedSettingsAsync().GetAwaiter().GetResult();

        AppDomain.CurrentDomain.UnhandledException += (sender, error) =>
        {
            Console.Error.WriteLine($"Fatal exception: {error.ExceptionObject}");
        };

        app.Run();
    }
}
