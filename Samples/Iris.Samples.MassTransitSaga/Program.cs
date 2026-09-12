using Iris.Samples.MassTransitSaga;
using Iris.Samples.MassTransitSaga.Contracts;
using MassTransit;
using MassTransit.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

Environment.SetEnvironmentVariable("MASSTRANSIT_USAGE_TELEMETRY", "false");

var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "http://127.0.0.1:4318";
// AddOtlpExporter takes the full signal URL, unlike UseOtlpExporter which appends the signal path itself.
var tracesEndpoint = new Uri($"{otlpEndpoint.TrimEnd('/')}/v1/traces");
var rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("Iris.Samples.MassTransitSaga"))
    // Traces only: the Iris receiver serves /v1/traces and nothing else, and UseOtlpExporter would
    // also export logs and metrics to endpoints that are not there.
    .WithTracing(t => t
        .AddSource(DiagnosticHeaders.DefaultListenerName)
        .AddOtlpExporter(o =>
        {
            o.Protocol = OtlpExportProtocol.HttpProtobuf;
            o.Endpoint = tracesEndpoint;
        }));

builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
    x.AddSagaStateMachine<OrderStateMachine, OrderState>().InMemoryRepository();
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbitHost, h =>
        {
            h.Username("guest");
            h.Password("guest");
        });
        cfg.ConfigureEndpoints(context);
    });
});

var host = builder.Build();
await host.StartAsync();

Console.WriteLine($"OrderStateMachine listening on queue 'order-state'; exporting traces to {tracesEndpoint}");
Console.WriteLine("Send SubmitOrder, AcceptOrder, ShipOrder or CancelOrder from Iris with MassTransit wrapping, or run with --demo.");

if (args.Contains("--demo"))
{
    var bus = host.Services.GetRequiredService<IBus>();
    var endpoint = await bus.GetSendEndpoint(new Uri("queue:order-state"));
    var orderId = Guid.NewGuid();
    Console.WriteLine($"Demo order {orderId}");
    await endpoint.Send(new SubmitOrder(orderId));
    await Task.Delay(1500);
    await endpoint.Send(new AcceptOrder(orderId));
    await Task.Delay(1500);
    await endpoint.Send(new ShipOrder(orderId));
}

await host.WaitForShutdownAsync();
