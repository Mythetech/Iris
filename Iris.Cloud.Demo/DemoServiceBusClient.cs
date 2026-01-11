using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Iris.Cloud.Demo.Contracts;
using Microsoft.Extensions.Logging;

namespace Iris.Cloud.Demo
{
    public class DemoServiceBusClient
    {
        public Action<ChangeColorsCommand>? ChangeColorsReceived;

        private readonly string _connectionString;
        private readonly ILogger<DemoServiceBusClient>? _logger;

        public DemoServiceBusClient(string connectionString, ILogger<DemoServiceBusClient>? logger = null)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        public async Task ConnectAsync()
        {
            string queueName = "changecolorscommand";

            // since ServiceBusClient implements IAsyncDisposable we create it with "await using"
            var client = new ServiceBusClient(_connectionString);

            // create the options to use for configuring the processor
            var options = new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 2,

            };

            // create a processor that we can use to process the messages
            ServiceBusProcessor processor = client.CreateProcessor(queueName, options);

            // configure the message and error handler to use
            processor.ProcessMessageAsync += MessageHandler;
            processor.ProcessErrorAsync += ErrorHandler;



            // start processing
            await processor.StartProcessingAsync();

            await Task.CompletedTask;

        }

        async Task MessageHandler(ProcessMessageEventArgs args)
        {
            string body = args.Message.Body.ToString();
            _logger?.LogDebug("Received message: {Body}", body);

            // we can evaluate application logic and use that to determine how to settle the message.
            await args.CompleteMessageAsync(args.Message);

            var evt = JsonSerializer.Deserialize<ChangeColorsCommand>(body);

            ChangeColorsReceived?.Invoke(evt!);
        }

        Task ErrorHandler(ProcessErrorEventArgs args)
        {
            _logger?.LogError(args.Exception,
                "Error processing message. Source: {ErrorSource}, Namespace: {Namespace}, EntityPath: {EntityPath}",
                args.ErrorSource, args.FullyQualifiedNamespace, args.EntityPath);
            return Task.CompletedTask;
        }

    }
}

