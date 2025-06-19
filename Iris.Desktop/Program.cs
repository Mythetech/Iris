using System.Reflection;
using Iris.Assemblies;
using Iris.Assemblies.CodeGeneration;
using Iris.Brokers;
using Iris.Brokers.Extensions;
using Iris.Brokers.Models;
using Iris.Components;
using Iris.Components.Infrastructure.MessageBus;
using Iris.Components.Settings;
using Iris.Desktop.Admin;
using Iris.Desktop.Brokers;
using Iris.Desktop.History;
using Iris.Desktop.Infrastructure;
using Iris.Desktop.PackageManagement;
using Iris.Desktop.Templates;
using Microsoft.Extensions.Configuration;
using Velopack;

namespace Iris.Desktop;

using Microsoft.Extensions.DependencyInjection;
using Photino.Blazor;

public class Program
{

    [STAThread]
    static void Main(string[] args)
    {
        VelopackApp.Build().Run();
        
        var builder = PhotinoBlazorAppBuilder.CreateDefault(args);

        builder.Services
            .AddLogging();

        var config = new ConfigurationManager();
        config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

        // register root component and selector
        builder.RootComponents.Add<App>("#app");

        builder.Services.AddIrisComponentServices<LocalConnectionManager, LocalConnectionManager, LocalTemplateService, LocalPackageService, LocalHistoryService, AdminClient, MessageLayoutRepository>();
        
        builder.Services.AddIrisConfiguration(config);

        builder.Services.AddIrisMessageBus(typeof(Program).Assembly);

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
        
        builder.Services.AddSettingsProviders(typeof(Program).Assembly);
        
        var app = builder.Build();

        app.Services.UseIrisMessageBus(typeof(Program).Assembly);

        app.MainWindow
            .SetTitle("Iris Desktop")
            .SetSize(1920, 1080)
            .SetTransparent(true);

        AppDomain.CurrentDomain.UnhandledException += (sender, error) =>
        {
            app.MainWindow.ShowMessage("Fatal exception", error.ExceptionObject.ToString());
        };

        

        app.Run();
    }
}