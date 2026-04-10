using System;
using Amazon;
using Amazon.SQS;
using Amazon.SQS.Model;
using Iris.Brokers.Exceptions;
using Iris.Brokers.Models;

namespace Iris.Brokers.Amazon
{
    public class AmazonWebServicesConnector : IConnector
    {
        public AmazonWebServicesConnector()
        {
        }

        public string Provider => "Amazon";

        public Task<IConnection?> ConnectAsync(ConnectionData data, bool discoverEndpoints = true)
            => ConnectAsync(data, CancellationToken.None, discoverEndpoints);

        public async Task<IConnection?> ConnectAsync(ConnectionData data, CancellationToken cancellationToken, bool discoverEndpoints = true)
        {
            RegionEndpoint regionEndpoint;

            try
            {
                regionEndpoint = RegionEndpoint.GetBySystemName(data.Region);
            }
            catch
            {
                throw new InvalidConnectionException("Invalid region endpoint");
            }

            AmazonSQSClient client;

            try
            {
                client = new AmazonSQSClient(data.Username, data.Password, regionEndpoint);
            }
            catch(Exception ex)
            {
                throw new InvalidConnectionException(ex.Message);
            }

            var queueResp = await client.ListQueuesAsync(new ListQueuesRequest(), cancellationToken);
            var profile = queueResp.QueueUrls.FirstOrDefault();
            Uri? uri = null;
            string? fullAddress = default;
            
            if (!string.IsNullOrEmpty(profile))
                uri = new Uri(profile);

            if (uri != null)
            {
                fullAddress= $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath.Substring(0, uri.AbsolutePath.LastIndexOf('/'))}";
            }


            var connection = new AmazonSimpleQueueServiceConnection(new ConnectionMetadata()
            {
                Connector = this,
                Address = fullAddress ?? $"{client.Config.RegionEndpoint.SystemName}.{client.Config.RegionEndpoint.PartitionDnsSuffix}",
            },
            client);

            if (discoverEndpoints && connection != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var endpoints = await connection.GetEndpointsAsync();
            }

            return connection;
        }
    }
}

