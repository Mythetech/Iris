using System.Globalization;
using Google.Api.Gax;
using Google.Api.Gax.Grpc;
using Google.Api.Gax.ResourceNames;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Grpc.Core;
using Iris.Brokers.Models;
using ConnectorTransports = Iris.Contracts.Brokers.Models.ConnectorTransports;
using EndpointPropertiesDto = Iris.Contracts.Brokers.Models.EndpointPropertiesDto;
using EndpointPropertyEntry = Iris.Contracts.Brokers.Models.EndpointPropertyEntry;
using PubSubReceivedMessage = Google.Cloud.PubSub.V1.ReceivedMessage;
using ReceivedMessage = Iris.Brokers.Models.ReceivedMessage;

namespace Iris.Brokers.Google;

public class PubSubConnection : IConnection, IHeaderCarrier, IMessageReceiver, IDeadLetterReceiver, IEndpointInspector
{
    public const string TopicType = "Topic";
    public const string SubscriptionType = "Subscription";
    public const string ProviderLabel = "GooglePubSub";

    // Pub/Sub holds a pull open on an empty subscription, so this is how long an empty
    // receive takes to report that there is nothing to read.
    private static readonly TimeSpan PullDeadline = TimeSpan.FromSeconds(5);

    // The SDK's own defaults retry an unreachable host for a minute before giving up.
    private static readonly TimeSpan AdminDeadline = TimeSpan.FromSeconds(15);

    private readonly ConnectionMetadata _metadata;
    private readonly GoogleConnectionSettings _settings;
    private readonly PublisherServiceApiClient _publisher;
    private readonly SubscriberServiceApiClient _subscriber;
    private List<EndpointDetails> _endpoints = new();

    public PubSubConnection(
        ConnectionMetadata metadata,
        GoogleConnectionSettings settings,
        PublisherServiceApiClient publisher,
        SubscriberServiceApiClient subscriber)
    {
        Connector = metadata.Connector;
        _metadata = metadata;
        _settings = settings;
        _publisher = publisher;
        _subscriber = subscriber;
    }

    public IConnector Connector { get; set; }

    public Guid Id { get; } = Guid.NewGuid();

    public string Name => ConnectorTransports.PubSub;

    public string Address => _metadata.Address;

    public int EndpointCount => _endpoints.Count;

    // Pub/Sub allows 100 attributes per message, keys up to 256 bytes that do not start
    // with "goog", and string values only.
    public int MaxHeaderCount => 100;

    public bool IsValidHeaderKey(string key)
        => !string.IsNullOrEmpty(key)
           && System.Text.Encoding.UTF8.GetByteCount(key) <= 256
           && !key.StartsWith("goog", StringComparison.Ordinal);

    public IReadOnlySet<HeaderDataType> SupportedDataTypes { get; } = new HashSet<HeaderDataType>
    {
        HeaderDataType.String,
    };

    // The API takes up to 1000 per pull. 100 is a cap for an interactive tool, not a limit
    // of the broker.
    public int MaxReceiveBatchSize => 100;

    public Task<List<EndpointDetails>> GetEndpointsAsync() => DiscoverAsync(CancellationToken.None);

    public async Task<List<EndpointDetails>> DiscoverAsync(CancellationToken cancellationToken)
    {
        var project = ProjectName.FromProject(_settings.ProjectId);
        var endpoints = new List<EndpointDetails>();

        try
        {
            await foreach (var topic in _publisher.ListTopicsAsync(project, callSettings: Admin(cancellationToken)))
                endpoints.Add(Endpoint(ShortName(topic.Name), TopicType));

            await foreach (var subscription in _subscriber.ListSubscriptionsAsync(project, callSettings: Admin(cancellationToken)))
                endpoints.Add(Endpoint(ShortName(subscription.Name), SubscriptionType));
        }
        catch (RpcException ex)
        {
            throw Translate(ex, PubSubOperation.Admin, cancellationToken);
        }

        return _endpoints = endpoints;
    }

    /// <summary>One cheap call that proves the credentials work, for a connect that skips discovery.</summary>
    public async Task ProbeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _publisher
                .ListTopicsAsync(ProjectName.FromProject(_settings.ProjectId), pageSize: 1, callSettings: Admin(cancellationToken))
                .ReadPageAsync(1, cancellationToken);
        }
        catch (RpcException ex)
        {
            throw Translate(ex, PubSubOperation.Admin, cancellationToken);
        }
    }

    public async Task SendAsync(EndpointDetails endpoint, MessageRequest message)
    {
        var pubsubMessage = new PubsubMessage { Data = ByteString.CopyFromUtf8(message.Json ?? string.Empty) };

        foreach (var header in message.Headers)
            pubsubMessage.Attributes[header.Key] = header.Value ?? string.Empty;

        try
        {
            await _publisher.PublishAsync(TopicPath(endpoint.Name), [pubsubMessage], Admin(CancellationToken.None));
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            // The service layer does not say whether the name is a topic or a subscription,
            // so the way to tell a subscription from a typo is to ask.
            var topic = await FindTopicOfSubscriptionAsync(endpoint.Name, CancellationToken.None);

            throw topic is null
                ? Translate(ex, PubSubOperation.Publish, CancellationToken.None)
                : new InvalidOperationException(
                    $"'{endpoint.Name}' is a subscription. Publish to its topic '{topic}' instead.", ex);
        }
        catch (RpcException ex)
        {
            throw Translate(ex, PubSubOperation.Publish, CancellationToken.None);
        }
    }

    public async Task<IReadOnlyList<ReceivedMessage>> ReceiveAsync(
        EndpointDetails endpoint, int count, CancellationToken cancellationToken = default)
    {
        try
        {
            return await PullAndAcknowledgeAsync(SubscriptionPath(endpoint.Name), count, ReadSource.Main, cancellationToken);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            throw await DescribeMissingSubscriptionAsync(endpoint.Name, ex, cancellationToken);
        }
        catch (RpcException ex)
        {
            throw Translate(ex, PubSubOperation.Pull, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<ReceivedMessage>> ReceiveDeadLetterAsync(
        EndpointDetails endpoint, int count, CancellationToken cancellationToken = default)
    {
        try
        {
            var subscription = await _subscriber.GetSubscriptionAsync(SubscriptionPath(endpoint.Name), Admin(cancellationToken));
            var deadLetterTopic = subscription.DeadLetterPolicy?.DeadLetterTopic;

            // No policy means no dead-letter topic exists, so "nothing" is the honest answer,
            // as it is for an SQS queue with no redrive policy.
            if (string.IsNullOrEmpty(deadLetterTopic))
                return [];

            var candidates = await ListTopicSubscriptionsAsync(deadLetterTopic, cancellationToken);

            return candidates.Count switch
            {
                // Not an empty list: Pub/Sub discards what is published to a topic with no
                // subscription, so "no dead letters" would be a lie.
                0 => throw new InvalidOperationException(
                    $"Dead-letter topic '{DisplayName(deadLetterTopic)}' has no subscription, so its messages cannot be read. Create a subscription on it in Google Cloud to read them."),
                1 => await PullAndAcknowledgeAsync(candidates[0], count, ReadSource.DeadLetter, cancellationToken),
                // The read is destructive, so picking one would take messages from a consumer
                // the user did not choose.
                _ => throw new InvalidOperationException(
                    $"Dead-letter topic '{DisplayName(deadLetterTopic)}' has several subscriptions: {string.Join(", ", candidates.Select(DisplayName))}. Receive from the one you want directly."),
            };
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            throw await DescribeMissingSubscriptionAsync(endpoint.Name, ex, cancellationToken);
        }
        catch (RpcException ex)
        {
            throw Translate(ex, PubSubOperation.Pull, cancellationToken);
        }
    }

    public async Task<EndpointPropertiesDto> InspectAsync(
        string endpointName, string? type, CancellationToken cancellationToken = default)
    {
        var entries = new List<EndpointPropertyEntry>();

        try
        {
            if (string.Equals(type, TopicType, StringComparison.OrdinalIgnoreCase))
            {
                var subscriptions = await ListTopicSubscriptionsAsync(TopicPath(endpointName), cancellationToken);

                entries.Add(new("Subscriptions",
                    subscriptions.Count == 0 ? null : string.Join(", ", subscriptions.Select(DisplayName))));
            }
            else
            {
                var subscription = await _subscriber.GetSubscriptionAsync(SubscriptionPath(endpointName), Admin(cancellationToken));
                var deadLetter = subscription.DeadLetterPolicy;

                entries.Add(new("Topic", DisplayName(subscription.Topic)));
                entries.Add(new("Delivery type", DeliveryType(subscription)));
                entries.Add(new("Ack deadline", $"{subscription.AckDeadlineSeconds.ToString(CultureInfo.InvariantCulture)}s"));
                entries.Add(new("Filter", NullIfEmpty(subscription.Filter)));
                entries.Add(new("Dead-letter topic",
                    string.IsNullOrEmpty(deadLetter?.DeadLetterTopic) ? null : DisplayName(deadLetter.DeadLetterTopic)));
                entries.Add(new("Max delivery attempts",
                    string.IsNullOrEmpty(deadLetter?.DeadLetterTopic) ? null : deadLetter.MaxDeliveryAttempts.ToString(CultureInfo.InvariantCulture)));
            }
        }
        catch (RpcException ex)
        {
            throw Translate(ex, PubSubOperation.Admin, cancellationToken);
        }

        return new EndpointPropertiesDto(entries);
    }

    private async Task<IReadOnlyList<ReceivedMessage>> PullAndAcknowledgeAsync(
        string subscription, int count, ReadSource source, CancellationToken cancellationToken)
    {
        PullResponse response;

        try
        {
            response = await _subscriber.PullAsync(
                subscription,
                Math.Clamp(count, 1, MaxReceiveBatchSize),
                CallSettings.FromExpiration(Expiration.FromTimeout(PullDeadline)).WithCancellationToken(cancellationToken));
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            return [];
        }

        if (response.ReceivedMessages.Count == 0)
            return [];

        // Pub/Sub has no receive-and-delete, so acknowledging what was pulled is what makes
        // the read destructive, the semantics every Iris receiver has.
        await _subscriber.AcknowledgeAsync(
            subscription, response.ReceivedMessages.Select(m => m.AckId), Admin(cancellationToken));

        return response.ReceivedMessages.Select(m => Map(m, subscription, source)).ToList();
    }

    private ReceivedMessage Map(PubSubReceivedMessage received, string subscription, ReadSource source)
    {
        var message = received.Message;
        var extra = new Dictionary<string, string> { ["Subscription"] = subscription };

        if (!string.IsNullOrEmpty(message.OrderingKey))
            extra["OrderingKey"] = message.OrderingKey;

        return new ReceivedMessage
        {
            Body = message.Data.ToStringUtf8(),
            MessageId = message.MessageId,
            Headers = new Dictionary<string, string>(message.Attributes),
            // Reported only for subscriptions with a dead-letter policy; zero otherwise.
            DeliveryCount = received.DeliveryAttempt > 0 ? received.DeliveryAttempt : null,
            EnqueuedTimeUtc = message.PublishTime?.ToDateTimeOffset(),
            SizeInBytes = message.Data.Length,
            Source = source,
            Provider = ProviderLabel,
            Native = new NativeMessageMetadata { Extra = extra },
        };
    }

    private async Task<string?> FindTopicOfSubscriptionAsync(string name, CancellationToken cancellationToken)
    {
        try
        {
            var subscription = await _subscriber.GetSubscriptionAsync(SubscriptionPath(name), Admin(cancellationToken));
            return DisplayName(subscription.Topic);
        }
        catch (RpcException)
        {
            return null;
        }
    }

    private async Task<Exception> DescribeMissingSubscriptionAsync(
        string name, RpcException notFound, CancellationToken cancellationToken)
    {
        List<string> subscriptions;

        try
        {
            // Asked for explicitly. Listing the subscriptions of a topic that does not exist is
            // not reliably an error, and an empty list would then read as "a topic with none".
            await _publisher.GetTopicAsync(TopicPath(name), Admin(cancellationToken));
            subscriptions = await ListTopicSubscriptionsAsync(TopicPath(name), cancellationToken);
        }
        catch (RpcException)
        {
            return Translate(notFound, PubSubOperation.Pull, cancellationToken);
        }

        return new InvalidOperationException(
            subscriptions.Count == 0
                ? $"'{name}' is a topic with no subscriptions, so nothing can be read from it."
                : $"'{name}' is a topic. Receive from one of its subscriptions: {string.Join(", ", subscriptions.Select(DisplayName))}.",
            notFound);
    }

    private async Task<List<string>> ListTopicSubscriptionsAsync(string topic, CancellationToken cancellationToken)
    {
        var subscriptions = new List<string>();

        await foreach (var subscription in _publisher.ListTopicSubscriptionsAsync(topic, callSettings: Admin(cancellationToken)))
            subscriptions.Add(subscription);

        return subscriptions;
    }

    private Exception Translate(RpcException exception, PubSubOperation operation, CancellationToken cancellationToken)
        => exception.StatusCode == StatusCode.Cancelled && cancellationToken.IsCancellationRequested
            ? new OperationCanceledException(cancellationToken)
            : new InvalidOperationException(PubSubErrors.Describe(exception, _settings, operation), exception);

    private static CallSettings Admin(CancellationToken cancellationToken)
        => CallSettings.FromExpiration(Expiration.FromTimeout(AdminDeadline)).WithCancellationToken(cancellationToken);

    private EndpointDetails Endpoint(string name, string type) => new()
    {
        Address = Address,
        Name = name,
        Provider = Connector.Provider,
        Type = type,
    };

    private string TopicPath(string name) => ResourcePath(name, TopicName.FormatProjectTopic);

    private string SubscriptionPath(string name) => ResourcePath(name, SubscriptionName.FormatProjectSubscription);

    // The SDK validates a name while formatting it and says so in its own words, and the UI
    // can ask for a send before any endpoint has been chosen.
    private string ResourcePath(string? name, Func<string, string, string> format)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException(PubSubErrors.EndpointNameRequired);

        try
        {
            return format(_settings.ProjectId, name);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException(PubSubErrors.EndpointNameInvalid(name), ex);
        }
    }

    /// <summary>
    /// The short ID for anything in this connection's project, and the full resource name
    /// for anything outside it, where the short ID alone would hide which project it is in.
    /// </summary>
    private string DisplayName(string resourceName)
        => resourceName.StartsWith($"projects/{_settings.ProjectId}/", StringComparison.Ordinal)
            ? ShortName(resourceName)
            : resourceName;

    private static string ShortName(string resourceName) => resourceName[(resourceName.LastIndexOf('/') + 1)..];

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static string DeliveryType(Subscription subscription)
    {
        if (!string.IsNullOrEmpty(subscription.PushConfig?.PushEndpoint)) return "Push";
        if (subscription.BigqueryConfig is not null) return "BigQuery";
        if (subscription.CloudStorageConfig is not null) return "Cloud Storage";
        return "Pull";
    }
}
