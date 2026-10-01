using Amazon;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using Iris.Brokers.Exceptions;
using Iris.Brokers.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using AwsAuthModes = Iris.Contracts.Brokers.Models.Amazon.AwsAuthModes;
using ConnectorProviders = Iris.Contracts.Brokers.Models.ConnectorProviders;

namespace Iris.Brokers.Amazon
{
    public class AmazonWebServicesConnector : IConnector
    {
        private readonly AwsCredentialsFactory _credentials;
        private readonly ILogger<AmazonWebServicesConnector> _logger;

        // Both parameters are optional so the container can build this without a registered
        // factory, and so callers that only need a connector for its metadata can write
        // new AmazonWebServicesConnector().
        public AmazonWebServicesConnector(AwsCredentialsFactory? credentials = null, ILoggerFactory? loggerFactory = null)
        {
            _credentials = credentials ?? new AwsCredentialsFactory();
            _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<AmazonWebServicesConnector>();
        }

        public string Provider => ConnectorProviders.Amazon;

        public Task<IConnection?> ConnectAsync(ConnectionData data, bool discoverEndpoints = true)
            => ConnectAsync(data, CancellationToken.None, discoverEndpoints);

        public async Task<IConnection?> ConnectAsync(ConnectionData data, CancellationToken cancellationToken, bool discoverEndpoints = true)
        {
            RegionEndpoint region;
            AWSCredentials credentials;

            try
            {
                // Credentials first: in profile mode the region may be left blank, so a missing
                // profile has to be reported before a missing region.
                credentials = _credentials.ResolveCredentials(data);
                region = _credentials.ResolveRegion(data);
            }
            catch (InvalidConnectionException ex) when (ex.InnerException is not null)
            {
                // The type only. A parse error from the AWS SDK quotes the line it could not
                // read, and that line can be the one holding a secret key.
                _logger.LogWarning("AWS connection settings could not be resolved: {Reason}", ex.InnerException.GetType().Name);
                throw;
            }

            try
            {
                var client = new AmazonSQSClient(credentials, region);
                var queues = await client.ListQueuesAsync(new ListQueuesRequest(), cancellationToken);

                var connection = new AmazonSimpleQueueServiceConnection(new ConnectionMetadata()
                {
                    Connector = this,
                    Address = AddressFor(queues.QueueUrls, region),
                },
                client);

                if (discoverEndpoints)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await connection.GetEndpointsAsync();
                }

                return connection;
            }
            catch (Exception ex) when (IsCallerCancellation(ex, cancellationToken))
            {
                throw;
            }
            catch (Exception ex)
            {
                // SSO, assume-role and console-login credentials are fetched on the first
                // call, so an expired session surfaces here and not when the client is built.
                _logger.LogWarning(ex, "Connecting to AWS in {Region} with {AuthMode} authentication failed",
                    region.SystemName, string.IsNullOrWhiteSpace(data.AuthMode) ? AwsAuthModes.AccessKeys : data.AuthMode);

                throw new InvalidConnectionException(AwsConnectionMessages.ForFailure(ex, data, region.SystemName), ex);
            }
        }

        /// <summary>
        /// The AWS SDK reports its own HTTP timeouts as cancellations. Only one the caller's
        /// token asked for is a cancellation; the rest are failed connections and get a message.
        /// </summary>
        public static bool IsCallerCancellation(Exception exception, CancellationToken cancellationToken)
            => exception is OperationCanceledException && cancellationToken.IsCancellationRequested;

        /// <summary>
        /// The address a connection is known by. A queue URL carries the account id, so it
        /// tells two accounts in one region apart; an account with no queues has only its
        /// region to go on. The SDK returns null, not an empty list, when there are no queues.
        /// </summary>
        public static string AddressFor(IReadOnlyList<string>? queueUrls, RegionEndpoint region)
        {
            var first = queueUrls?.FirstOrDefault();

            if (string.IsNullOrEmpty(first))
                return $"{region.SystemName}.{region.PartitionDnsSuffix}";

            var uri = new Uri(first);
            return $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath[..uri.AbsolutePath.LastIndexOf('/')]}";
        }
    }
}
