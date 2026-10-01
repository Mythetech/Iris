using FluentAssertions;
using Google.Api.Gax;
using Google.Api.Gax.Grpc;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Iris.Brokers.Google;
using Iris.Brokers.Models;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using PubSubReceivedMessage = Google.Cloud.PubSub.V1.ReceivedMessage;

namespace Iris.Brokers.Test.Google;

/// <summary>
/// The decisions <see cref="PubSubConnection"/> makes around its SDK calls, with the SDK
/// clients substituted. What Pub/Sub itself does with those calls is the emulator suite's
/// business, in <c>Iris.Integration.Tests</c>.
/// </summary>
public class PubSubConnectionTests
{
    private const string Project = "my-project";
    private const string OrdersTopic = "projects/my-project/topics/orders";
    private const string OrdersSubscription = "projects/my-project/subscriptions/orders-sub";

    private readonly PublisherServiceApiClient _publisher = Substitute.For<PublisherServiceApiClient>();
    private readonly SubscriberServiceApiClient _subscriber = Substitute.For<SubscriberServiceApiClient>();

    private PubSubConnection Connection()
    {
        var connector = Substitute.For<IConnector>();
        connector.Provider.Returns("Google");
        var settings = new GoogleConnectionSettings(
            GoogleCredentialSource.ApplicationDefault, Project, $"pubsub.googleapis.com/projects/{Project}");

        return new PubSubConnection(
            new ConnectionMetadata { Connector = connector, Address = settings.Address }, settings, _publisher, _subscriber);
    }

    private static EndpointDetails Endpoint(string name) => new()
    {
        Address = $"pubsub.googleapis.com/projects/{Project}",
        Provider = "Google",
        Name = name,
        // What LocalConnectionManager passes for every read, whatever the endpoint really is.
        Type = "Queue",
    };

    private static RpcException Rpc(StatusCode code) => new(new Status(code, "the SDK's own wording"));

    private static PubSubReceivedMessage Received(string ackId, string body) => new()
    {
        AckId = ackId,
        DeliveryAttempt = 0,
        Message = new PubsubMessage
        {
            MessageId = "id-" + ackId,
            Data = ByteString.CopyFromUtf8(body),
            PublishTime = Timestamp.FromDateTimeOffset(new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero)),
            Attributes = { ["customAttr"] = "hello" },
        },
    };

    /// <summary>
    /// The SDK returns lists as a paged async sequence, an abstract class with no public way
    /// to build one from items. This is the smallest thing that can stand in for it.
    /// </summary>
    private sealed class Listed(params string[] items) : PagedAsyncEnumerable<ListTopicSubscriptionsResponse, string>
    {
        public override async IAsyncEnumerator<string> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            foreach (var item in items)
            {
                await Task.Yield();
                yield return item;
            }
        }
    }

    private void TopicHasSubscriptions(string topic, params string[] subscriptions)
        => _publisher
            .ListTopicSubscriptionsAsync(topic, Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CallSettings>())
            .Returns(new Listed(subscriptions));

    private void SubscriptionDeadLettersTo(string deadLetterTopic)
        => _subscriber
            .GetSubscriptionAsync(OrdersSubscription, Arg.Any<CallSettings>())
            .Returns(new Subscription
            {
                Name = OrdersSubscription,
                Topic = OrdersTopic,
                DeadLetterPolicy = new DeadLetterPolicy { DeadLetterTopic = deadLetterTopic, MaxDeliveryAttempts = 5 },
            });

    [Fact]
    public async Task Send_publishes_the_body_and_every_header_as_an_attribute()
    {
        PubsubMessage? published = null;
        _publisher
            .PublishAsync(OrdersTopic, Arg.Do<IEnumerable<PubsubMessage>>(m => published = m.Single()), Arg.Any<CallSettings>())
            .Returns(new PublishResponse());
        var request = MessageRequest.Create("orders", "{\"i\":1}", generateIrisHeaders: false,
            headers: new Dictionary<string, string> { ["customAttr"] = "hello", ["count"] = "42" });

        await Connection().SendAsync(Endpoint("orders"), request);

        published.Should().NotBeNull();
        published!.Data.ToStringUtf8().Should().Be("{\"i\":1}");
        published.Attributes.Should().BeEquivalentTo(new Dictionary<string, string> { ["customAttr"] = "hello", ["count"] = "42" });
    }

    [Fact]
    public async Task Send_to_a_subscription_names_the_topic_to_publish_to()
    {
        _publisher
            .PublishAsync("projects/my-project/topics/orders-sub", Arg.Any<IEnumerable<PubsubMessage>>(), Arg.Any<CallSettings>())
            .ThrowsAsync(Rpc(StatusCode.NotFound));
        _subscriber
            .GetSubscriptionAsync(OrdersSubscription, Arg.Any<CallSettings>())
            .Returns(new Subscription { Name = OrdersSubscription, Topic = OrdersTopic });

        var act = () => Connection().SendAsync(Endpoint("orders-sub"), MessageRequest.Create("orders-sub", "{}", false));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("'orders-sub' is a subscription. Publish to its topic 'orders' instead.");
    }

    [Fact]
    public async Task Send_to_a_name_that_is_nothing_reports_not_found()
    {
        _publisher
            .PublishAsync(Arg.Any<string>(), Arg.Any<IEnumerable<PubsubMessage>>(), Arg.Any<CallSettings>())
            .ThrowsAsync(Rpc(StatusCode.NotFound));
        _subscriber
            .GetSubscriptionAsync(Arg.Any<string>(), Arg.Any<CallSettings>())
            .ThrowsAsync(Rpc(StatusCode.NotFound));

        var act = () => Connection().SendAsync(Endpoint("typo"), MessageRequest.Create("typo", "{}", false));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Project, topic, or subscription not found. Check the project ID 'my-project'.");
    }

    [Fact]
    public async Task Send_of_a_message_pubsub_rejects_explains_the_limits()
    {
        _publisher
            .PublishAsync(Arg.Any<string>(), Arg.Any<IEnumerable<PubsubMessage>>(), Arg.Any<CallSettings>())
            .ThrowsAsync(Rpc(StatusCode.InvalidArgument));

        var act = () => Connection().SendAsync(Endpoint("orders"), MessageRequest.Create("orders", "", false));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Pub/Sub rejected the message. A message needs a body or at least one attribute, attribute values are limited to 1024 bytes, and a message to 10 MB.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task With_no_endpoint_chosen_a_send_or_receive_asks_for_one_instead_of_quoting_the_sdk(string? name)
    {
        var send = () => Connection().SendAsync(Endpoint(name!), MessageRequest.Create("orders", "{}", false));
        var receive = () => Connection().ReceiveAsync(Endpoint(name!), 10, TestContext.Current.CancellationToken);
        var deadLetter = () => Connection().ReceiveDeadLetterAsync(Endpoint(name!), 10, TestContext.Current.CancellationToken);

        (await send.Should().ThrowAsync<InvalidOperationException>()).WithMessage(PubSubErrors.EndpointNameRequired);
        (await receive.Should().ThrowAsync<InvalidOperationException>()).WithMessage(PubSubErrors.EndpointNameRequired);
        (await deadLetter.Should().ThrowAsync<InvalidOperationException>()).WithMessage(PubSubErrors.EndpointNameRequired);
    }

    [Fact]
    public async Task A_name_pubsub_cannot_address_is_refused_with_fixed_wording()
    {
        var send = () => Connection().SendAsync(Endpoint("orders/extra"), MessageRequest.Create("orders", "{}", false));
        var receive = () => Connection().ReceiveAsync(Endpoint("orders/extra"), 10, TestContext.Current.CancellationToken);

        (await send.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("'orders/extra' is not a valid Pub/Sub topic or subscription name.");
        (await receive.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("'orders/extra' is not a valid Pub/Sub topic or subscription name.");
    }

    [Fact]
    public async Task Receive_acknowledges_exactly_what_it_pulled_and_maps_it()
    {
        _subscriber
            .PullAsync(OrdersSubscription, 10, Arg.Any<CallSettings>())
            .Returns(new PullResponse { ReceivedMessages = { Received("a", "{\"i\":1}"), Received("b", "{\"i\":2}") } });
        List<string>? acknowledged = null;
        _subscriber
            .AcknowledgeAsync(OrdersSubscription, Arg.Do<IEnumerable<string>>(ids => acknowledged = ids.ToList()), Arg.Any<CallSettings>())
            .Returns(Task.CompletedTask);

        var messages = await Connection().ReceiveAsync(Endpoint("orders-sub"), 10, TestContext.Current.CancellationToken);

        acknowledged.Should().Equal("a", "b");
        messages.Should().HaveCount(2);
        var first = messages[0];
        first.Body.Should().Be("{\"i\":1}");
        first.MessageId.Should().Be("id-a");
        first.Headers.Should().Contain("customAttr", "hello");
        first.DeliveryCount.Should().BeNull("Pub/Sub reports zero attempts unless the subscription has a dead-letter policy");
        first.EnqueuedTimeUtc.Should().Be(new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero));
        first.SizeInBytes.Should().Be(7);
        first.Source.Should().Be(ReadSource.Main);
        first.Provider.Should().Be("GooglePubSub");
        first.Native!.Extra.Should().Contain("Subscription", OrdersSubscription);
    }

    [Fact]
    public async Task Receive_on_an_empty_subscription_is_an_empty_list_not_a_timeout()
    {
        _subscriber
            .PullAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CallSettings>())
            .ThrowsAsync(Rpc(StatusCode.DeadlineExceeded));

        var messages = await Connection().ReceiveAsync(Endpoint("orders-sub"), 10, TestContext.Current.CancellationToken);

        messages.Should().BeEmpty();
        await _subscriber.DidNotReceive().AcknowledgeAsync(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CallSettings>());
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(25, 25)]
    [InlineData(5000, 100)]
    public async Task Receive_asks_for_between_one_and_the_batch_cap(int requested, int expected)
    {
        _subscriber
            .PullAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CallSettings>())
            .Returns(new PullResponse());

        await Connection().ReceiveAsync(Endpoint("orders-sub"), requested, TestContext.Current.CancellationToken);

        await _subscriber.Received(1).PullAsync(OrdersSubscription, expected, Arg.Any<CallSettings>());
    }

    [Fact]
    public async Task Receive_cancelled_by_the_caller_surfaces_as_cancellation()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        _subscriber
            .PullAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CallSettings>())
            .ThrowsAsync(Rpc(StatusCode.Cancelled));

        var act = () => Connection().ReceiveAsync(Endpoint("orders-sub"), 10, cancelled.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Receive_from_a_push_subscription_says_it_cannot_be_read()
    {
        _subscriber
            .PullAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CallSettings>())
            .ThrowsAsync(Rpc(StatusCode.FailedPrecondition));

        var act = () => Connection().ReceiveAsync(Endpoint("pushy"), 10, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("This subscription does not deliver by pull, so it cannot be read.");
    }

    [Fact]
    public async Task Dead_letter_receive_without_a_policy_is_empty_and_pulls_nothing()
    {
        _subscriber
            .GetSubscriptionAsync(OrdersSubscription, Arg.Any<CallSettings>())
            .Returns(new Subscription { Name = OrdersSubscription, Topic = OrdersTopic });

        var messages = await Connection().ReceiveDeadLetterAsync(Endpoint("orders-sub"), 10, TestContext.Current.CancellationToken);

        messages.Should().BeEmpty();
        await _subscriber.DidNotReceive().PullAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CallSettings>());
    }

    [Fact]
    public async Task Receive_from_a_topic_name_lists_the_subscriptions_to_read_instead()
    {
        _subscriber
            .PullAsync("projects/my-project/subscriptions/orders", Arg.Any<int>(), Arg.Any<CallSettings>())
            .ThrowsAsync(Rpc(StatusCode.NotFound));
        TopicHasSubscriptions(OrdersTopic, "projects/my-project/subscriptions/billing", "projects/my-project/subscriptions/shipping");

        var act = () => Connection().ReceiveAsync(Endpoint("orders"), 10, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("'orders' is a topic. Receive from one of its subscriptions: billing, shipping.");
    }

    [Fact]
    public async Task Receive_from_a_name_that_is_nothing_reports_not_found()
    {
        _subscriber
            .PullAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CallSettings>())
            .ThrowsAsync(Rpc(StatusCode.NotFound));
        _publisher
            .GetTopicAsync("projects/my-project/topics/typo", Arg.Any<CallSettings>())
            .ThrowsAsync(Rpc(StatusCode.NotFound));
        // What the emulator answers for a topic that does not exist: no error, no items.
        TopicHasSubscriptions("projects/my-project/topics/typo");

        var act = () => Connection().ReceiveAsync(Endpoint("typo"), 10, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Project, topic, or subscription not found. Check the project ID 'my-project'.");
    }

    [Fact]
    public async Task Dead_letter_receive_reads_the_only_subscription_on_the_dead_letter_topic()
    {
        const string deadTopic = "projects/my-project/topics/orders-dead";
        const string deadReader = "projects/my-project/subscriptions/dead-reader";
        SubscriptionDeadLettersTo(deadTopic);
        TopicHasSubscriptions(deadTopic, deadReader);
        _subscriber
            .PullAsync(deadReader, 10, Arg.Any<CallSettings>())
            .Returns(new PullResponse { ReceivedMessages = { Received("a", "{\"dead\":true}") } });

        var messages = await Connection().ReceiveDeadLetterAsync(Endpoint("orders-sub"), 10, TestContext.Current.CancellationToken);

        var message = messages.Should().ContainSingle().Which;
        message.Source.Should().Be(ReadSource.DeadLetter);
        message.Native!.Extra.Should().Contain("Subscription", deadReader);
        await _subscriber.Received(1).AcknowledgeAsync(deadReader, Arg.Any<IEnumerable<string>>(), Arg.Any<CallSettings>());
    }

    [Fact]
    public async Task Dead_letter_receive_explains_a_dead_letter_topic_nothing_subscribes_to()
    {
        const string deadTopic = "projects/my-project/topics/orders-dead";
        SubscriptionDeadLettersTo(deadTopic);
        TopicHasSubscriptions(deadTopic);

        var act = () => Connection().ReceiveDeadLetterAsync(Endpoint("orders-sub"), 10, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Dead-letter topic 'orders-dead' has no subscription, so its messages cannot be read. Create a subscription on it in Google Cloud to read them.");
    }

    [Fact]
    public async Task Dead_letter_receive_refuses_to_guess_between_several_subscriptions()
    {
        const string deadTopic = "projects/other-project/topics/orders-dead";
        SubscriptionDeadLettersTo(deadTopic);
        TopicHasSubscriptions(deadTopic, "projects/other-project/subscriptions/audit", "projects/my-project/subscriptions/replay");

        var act = () => Connection().ReceiveDeadLetterAsync(Endpoint("orders-sub"), 10, TestContext.Current.CancellationToken);

        // Anything outside this connection's project is named in full, because its short name
        // alone would hide which project it lives in.
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Dead-letter topic 'projects/other-project/topics/orders-dead' has several subscriptions: projects/other-project/subscriptions/audit, replay. Receive from the one you want directly.");
        await _subscriber.DidNotReceive().PullAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CallSettings>());
    }

    [Fact]
    public async Task Inspecting_a_subscription_lists_the_same_rows_whether_or_not_they_are_set()
    {
        _subscriber
            .GetSubscriptionAsync(OrdersSubscription, Arg.Any<CallSettings>())
            .Returns(new Subscription { Name = OrdersSubscription, Topic = OrdersTopic, AckDeadlineSeconds = 10 });

        var properties = (await Connection().InspectAsync("orders-sub", "Subscription", TestContext.Current.CancellationToken)).Properties;

        properties.Select(p => (p.Key, p.Value)).Should().Equal(
            ("Topic", "orders"),
            ("Delivery type", "Pull"),
            ("Ack deadline", "10s"),
            ("Filter", null),
            ("Dead-letter topic", null),
            ("Max delivery attempts", null));
    }

    [Fact]
    public async Task Inspecting_a_subscription_names_a_dead_letter_topic_in_another_project_in_full()
    {
        _subscriber
            .GetSubscriptionAsync(OrdersSubscription, Arg.Any<CallSettings>())
            .Returns(new Subscription
            {
                Name = OrdersSubscription,
                Topic = OrdersTopic,
                AckDeadlineSeconds = 30,
                Filter = "attributes.kind = \"order\"",
                PushConfig = new PushConfig { PushEndpoint = "https://example.com/push" },
                DeadLetterPolicy = new DeadLetterPolicy { DeadLetterTopic = "projects/other-project/topics/dead", MaxDeliveryAttempts = 5 },
            });

        var properties = (await Connection().InspectAsync("orders-sub", "Subscription", TestContext.Current.CancellationToken)).Properties;

        properties.Select(p => (p.Key, p.Value)).Should().Equal(
            ("Topic", "orders"),
            ("Delivery type", "Push"),
            ("Ack deadline", "30s"),
            ("Filter", "attributes.kind = \"order\""),
            ("Dead-letter topic", "projects/other-project/topics/dead"),
            ("Max delivery attempts", "5"));
    }
}
