using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Iris.Cloud.Demo.Contracts;
namespace Iris.Cloud.Demo
{
    public class DemoServiceBusClient
    {
        public Action<ChangeColorsCommand> ChangeColorsReceived;

        private string _connectionString = "";

        public DemoServiceBusClient(string connectionString)
        {
            _connectionString = connectionString;
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
            Console.WriteLine(body);

            // we can evaluate application logic and use that to determine how to settle the message.
            await args.CompleteMessageAsync(args.Message);

            var evt = JsonSerializer.Deserialize<ChangeColorsCommand>(body);

            ChangeColorsReceived?.Invoke(evt);
        }

        Task ErrorHandler(ProcessErrorEventArgs args)
        {
            // the error source tells me at what point in the processing an error occurred
            Console.WriteLine(args.ErrorSource);
            // the fully qualified namespace is available
            Console.WriteLine(args.FullyQualifiedNamespace);
            // as well as the entity path
            Console.WriteLine(args.EntityPath);
            Console.WriteLine(args.Exception.ToString());
            return Task.CompletedTask;
        }

    }
}

