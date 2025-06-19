using Iris.Cloud.Demo;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor.Services;
using MassTransit;
using NServiceBus;
using Iris.Cloud.Demo.Contracts;
using NServiceBus.Azure.Transports.WindowsAzureStorageQueues;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddMudServices();

var connectionString = builder.Configuration.GetConnectionString("AzureServiceBus");

var client = new DemoServiceBusClient(connectionString);

builder.Services.AddSingleton(client);
builder.Services.AddSingleton<IConsumerNotifier<ChangeColorsCommand>, ChangeColorsCommandNotifier>();
builder.Services.AddSingleton<IConsumerNotifier<ChangeColorsCommandV2>, ChangeColorsCommandV2Notifier>();

var ec = new EndpointConfiguration("changecolorscommand");
ec.UseSerialization<SystemJsonSerializer>();
var transport = new AzureStorageQueueTransport("REDACTED_AZURE_STORAGE_CONNECTION_STRING",
    false);
ec.Recoverability().Delayed(settings => settings.NumberOfRetries(0));


ec.UseTransport(transport);

var endpointInstance = NServiceBus.EndpointWithExternallyManagedContainer.Create(ec, builder.Services);

var ec2 = new EndpointConfiguration("changecolorscommand-2");
ec2.UseSerialization<SystemJsonSerializer>();
ec2.Recoverability().Delayed(settings => settings.NumberOfRetries(0));
ec2.UseTransport(transport);

var endpointInstance2 = await NServiceBus.Endpoint.Start(ec2)
    .ConfigureAwait(false);


builder.Services.AddSingleton<IMessageSession>(endpointInstance2);


builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<MassTransitConsumer>();

    x.UsingAzureServiceBus((context, cfg) =>
    {
        cfg.Host(connectionString);

        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();

var ei = await endpointInstance.Start(app.Services);

if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

var message = new ChangeColorsCommand(Red: 200, Green: 0, Blue: 0);

await ei.SendLocal(message);

app.Run();

