using FluentAssertions;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Grpc.Core;
using Iris.Brokers;
using Iris.Brokers.Exceptions;
using Iris.Brokers.Google;
using Iris.Brokers.Models;
using Iris.Integration.Tests.Fixtures;

namespace Iris.Integration.Tests.Brokers;

/// <summary>
/// Emulator-backed tests for <see cref="PubSubConnection"/>, connected the way the app
/// connects: through <see cref="GoogleConnector"/> with an emulator host.
///
/// Every test works in a project of its own. The emulator creates a project on first use and
/// keeps them apart, so no test can see another's topics, and discovery can assert on the
/// whole endpoint list rather than searching it.
/// </summary>
[Collection("PubSubEmulator")]
[Trait("Category", TestCategories.Container)]
public class GooglePubSubContainerTests
{
    private readonly PubSubEmulatorFixture _emulator;
    private readonly string _project = $"iris-{Guid.NewGuid():N}";

    public GooglePubSubContainerTests(PubSubEmulatorFixture emulator)
    {
        _emulator = emulator;
    }

    private async Task<PubSubConnection> ConnectAsync(CancellationToken cancellation)
        => (PubSubConnection)(await new GoogleConnector().ConnectAsync(
            new ConnectionData { CredentialSource = "Emulator", Uri = _emulator.Host, ProjectId = _project }, cancellation))!;

    private PublisherServiceApiClient Publisher() => new PublisherServiceApiClientBuilder
    {
        Endpoint = _emulator.Host,
        ChannelCredentials = ChannelCredentials.Insecure,
    }.Build();

    private SubscriberServiceApiClient Subscriber() => new SubscriberServiceApiClientBuilder
    {
        Endpoint = _emulator.Host,
        ChannelCredentials = ChannelCredentials.Insecure,
    }.Build();

    private string Topic(string name) => TopicName.FormatProjectTopic(_project, name);

    private string Subscription(string name) => SubscriptionName.FormatProjectSubscription(_project, name);

    private Task CreateTopicAsync(string name) => Publisher().CreateTopicAsync(Topic(name));

    private Task CreateSubscriptionAsync(string name, string topic, string? deadLetterTopic = null)
    {
        var subscription = new Subscription { Name = Subscription(name), Topic = Topic(topic) };

        if (deadLetterTopic is not null)
        {
            subscription.DeadLetterPolicy = new DeadLetterPolicy
            {
                DeadLetterTopic = Topic(deadLetterTopic),
                MaxDeliveryAttempts = 5,
            };
        }

        return Subscriber().CreateSubscriptionAsync(subscription);
    }

    private EndpointDetails Endpoint(string name) => new()
    {
        Provider = "Google",
        Address = $"{_emulator.Host}/projects/{_project}",
        Name = name,
        // Deliberately what LocalConnectionManager sends for a read: the connection must work
        // out for itself whether the name is a topic or a subscription.
        Type = "Queue",
    };

    [Fact(DisplayName = "Connecting discovers topics and subscriptions with their types", Timeout = 120000)]
    public async Task Connect_discovers_topics_and_subscriptions()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await CreateTopicAsync("orders");
        await CreateSubscriptionAsync("orders-sub", "orders");

        var connection = await ConnectAsync(cancellation);
        var endpoints = await connection.GetEndpointsAsync();

        connection.Name.Should().Be("PubSub");
        connection.Address.Should().Be($"{_emulator.Host}/projects/{_project}");
        connection.EndpointCount.Should().Be(2);
        endpoints.Select(e => (e.Type, e.Name, e.Address)).Should().Equal(
            ("Topic", "orders", connection.Address),
            ("Subscription", "orders-sub", connection.Address));
    }

    [Fact(DisplayName = "A project with nothing in it connects with no endpoints", Timeout = 120000)]
    public async Task Connect_to_an_empty_project()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var connection = await ConnectAsync(cancellation);

        (await connection.GetEndpointsAsync()).Should().BeEmpty();
    }

    [Fact(DisplayName = "Connecting without discovery still proves the emulator is there", Timeout = 120000)]
    public async Task Connect_without_discovery()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var connection = await new GoogleConnector().ConnectAsync(
            new ConnectionData { Uri = _emulator.Host, ProjectId = _project }, cancellation, discoverEndpoints: false);

        connection.Should().NotBeNull();
        connection!.EndpointCount.Should().Be(0);
    }

    [Fact(DisplayName = "A published message is received once, with its body and attributes", Timeout = 120000)]
    public async Task Send_then_receive_round_trips_body_and_attributes()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await CreateTopicAsync("orders");
        await CreateSubscriptionAsync("orders-sub", "orders");
        var connection = await ConnectAsync(cancellation);
        var request = MessageRequest.Create("orders", "{\"i\":1}", generateIrisHeaders: false,
            headers: new Dictionary<string, string> { ["customAttr"] = "hello" });

        await connection.SendAsync(Endpoint("orders"), request);
        var messages = await connection.ReceiveAsync(Endpoint("orders-sub"), 10, cancellation);

        messages.Should().ContainSingle();
        var message = messages[0];
        message.Body.Should().Be("{\"i\":1}");
        message.Headers.Should().Contain("customAttr", "hello");
        message.MessageId.Should().NotBeNullOrWhiteSpace();
        message.EnqueuedTimeUtc.Should().NotBeNull();
        message.SizeInBytes.Should().Be(7);
        message.Source.Should().Be(ReadSource.Main);
        message.Provider.Should().Be("GooglePubSub");
        message.Native!.Extra.Should().Contain("Subscription", Subscription("orders-sub"));

        // Acknowledged, so it is gone: the receive was destructive.
        (await connection.ReceiveAsync(Endpoint("orders-sub"), 10, cancellation)).Should().BeEmpty();
    }

    [Fact(DisplayName = "Receiving from an empty subscription returns nothing rather than failing", Timeout = 120000)]
    public async Task Receive_from_an_empty_subscription()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await CreateTopicAsync("quiet");
        await CreateSubscriptionAsync("quiet-sub", "quiet");
        var connection = await ConnectAsync(cancellation);

        (await connection.ReceiveAsync(Endpoint("quiet-sub"), 10, cancellation)).Should().BeEmpty();
    }

    [Fact(DisplayName = "Cancelling a receive that is waiting on an empty subscription surfaces as cancellation", Timeout = 120000)]
    public async Task Cancelling_a_waiting_receive_is_cancellation_not_a_failure()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await CreateTopicAsync("quiet");
        await CreateSubscriptionAsync("quiet-sub", "quiet");
        var connection = await ConnectAsync(cancellation);
        using var impatient = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        impatient.CancelAfter(TimeSpan.FromMilliseconds(500));

        var act = () => connection.ReceiveAsync(Endpoint("quiet-sub"), 10, impatient.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact(DisplayName = "A message with no body and no attributes is refused with an explanation", Timeout = 120000)]
    public async Task An_empty_message_is_refused_with_an_explanation()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await CreateTopicAsync("orders");
        var connection = await ConnectAsync(cancellation);

        var act = () => connection.SendAsync(Endpoint("orders"), MessageRequest.Create("orders", "", false));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Pub/Sub rejected the message. A message needs a body or at least one attribute, attribute values are limited to 1024 bytes, and a message to 10 MB.");
    }

    [Fact(DisplayName = "Receiving from a topic name fails and lists its subscriptions", Timeout = 120000)]
    public async Task Receive_from_a_topic_lists_its_subscriptions()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await CreateTopicAsync("orders");
        await CreateSubscriptionAsync("billing", "orders");
        await CreateSubscriptionAsync("shipping", "orders");
        var connection = await ConnectAsync(cancellation);

        var act = () => connection.ReceiveAsync(Endpoint("orders"), 10, cancellation);

        var message = (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message;
        message.Should().StartWith("'orders' is a topic. Receive from one of its subscriptions: ");
        message.Should().Contain("billing").And.Contain("shipping");
    }

    [Fact(DisplayName = "Receiving from a topic with no subscriptions says nothing can be read", Timeout = 120000)]
    public async Task Receive_from_a_topic_without_subscriptions()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await CreateTopicAsync("lonely");
        var connection = await ConnectAsync(cancellation);

        var act = () => connection.ReceiveAsync(Endpoint("lonely"), 10, cancellation);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("'lonely' is a topic with no subscriptions, so nothing can be read from it.");
    }

    [Fact(DisplayName = "Receiving from a name that is nothing reports not found", Timeout = 120000)]
    public async Task Receive_from_nothing()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var connection = await ConnectAsync(cancellation);

        var act = () => connection.ReceiveAsync(Endpoint("typo"), 10, cancellation);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage($"Project, topic, or subscription not found. Check the project ID '{_project}'.");
    }

    [Fact(DisplayName = "Sending to a subscription name fails and names its topic", Timeout = 120000)]
    public async Task Send_to_a_subscription_names_its_topic()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await CreateTopicAsync("orders");
        await CreateSubscriptionAsync("orders-sub", "orders");
        var connection = await ConnectAsync(cancellation);

        var act = () => connection.SendAsync(Endpoint("orders-sub"), MessageRequest.Create("orders-sub", "{}", false));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("'orders-sub' is a subscription. Publish to its topic 'orders' instead.");
    }

    [Fact(DisplayName = "A topic and a subscription with the same name are each used for what they can do", Timeout = 120000)]
    public async Task A_shared_name_publishes_to_the_topic_and_pulls_from_the_subscription()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await CreateTopicAsync("orders");
        await CreateSubscriptionAsync("orders", "orders");
        var connection = await ConnectAsync(cancellation);

        await connection.SendAsync(Endpoint("orders"), MessageRequest.Create("orders", "{\"same\":true}", false));
        var messages = await connection.ReceiveAsync(Endpoint("orders"), 10, cancellation);

        messages.Should().ContainSingle().Which.Body.Should().Be("{\"same\":true}");
    }

    [Fact(DisplayName = "Dead-letter receive is empty when the subscription has no dead-letter policy", Timeout = 120000)]
    public async Task Dead_letter_without_a_policy_is_empty()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await CreateTopicAsync("orders");
        await CreateSubscriptionAsync("orders-sub", "orders");
        var connection = await ConnectAsync(cancellation);

        (await connection.ReceiveDeadLetterAsync(Endpoint("orders-sub"), 10, cancellation)).Should().BeEmpty();
    }

    [Fact(DisplayName = "Dead-letter receive reads the one subscription on the dead-letter topic", Timeout = 120000)]
    public async Task Dead_letter_reads_the_only_subscription_on_the_dead_letter_topic()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await CreateTopicAsync("orders");
        await CreateTopicAsync("orders-dead");
        await CreateSubscriptionAsync("orders-sub", "orders", deadLetterTopic: "orders-dead");
        await CreateSubscriptionAsync("dead-reader", "orders-dead");
        var connection = await ConnectAsync(cancellation);

        // Published straight to the dead-letter topic. What is under test is where Iris looks
        // for dead letters, not Pub/Sub's own redelivery counting, which the emulator would
        // need five failed deliveries and their backoff to demonstrate.
        await Publisher().PublishAsync(
            Topic("orders-dead"),
            [new PubsubMessage { Data = ByteString.CopyFromUtf8("{\"dead\":true}") }]);

        var messages = await connection.ReceiveDeadLetterAsync(Endpoint("orders-sub"), 10, cancellation);

        var message = messages.Should().ContainSingle().Which;
        message.Body.Should().Be("{\"dead\":true}");
        message.Source.Should().Be(ReadSource.DeadLetter);
        message.Native!.Extra.Should().Contain("Subscription", Subscription("dead-reader"));
    }

    [Fact(DisplayName = "Dead-letter receive explains a dead-letter topic nothing subscribes to", Timeout = 120000)]
    public async Task Dead_letter_topic_without_a_subscription_is_explained()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await CreateTopicAsync("orders");
        await CreateTopicAsync("orders-dead");
        await CreateSubscriptionAsync("orders-sub", "orders", deadLetterTopic: "orders-dead");
        var connection = await ConnectAsync(cancellation);

        var act = () => connection.ReceiveDeadLetterAsync(Endpoint("orders-sub"), 10, cancellation);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Dead-letter topic 'orders-dead' has no subscription, so its messages cannot be read. Create a subscription on it in Google Cloud to read them.");
    }

    [Fact(DisplayName = "Dead-letter receive refuses to guess between several subscriptions", Timeout = 120000)]
    public async Task Dead_letter_topic_with_several_subscriptions_is_not_guessed()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await CreateTopicAsync("orders");
        await CreateTopicAsync("orders-dead");
        await CreateSubscriptionAsync("orders-sub", "orders", deadLetterTopic: "orders-dead");
        await CreateSubscriptionAsync("dead-reader-a", "orders-dead");
        await CreateSubscriptionAsync("dead-reader-b", "orders-dead");
        var connection = await ConnectAsync(cancellation);
        await Publisher().PublishAsync(
            Topic("orders-dead"),
            [new PubsubMessage { Data = ByteString.CopyFromUtf8("{\"dead\":true}") }]);

        var act = () => connection.ReceiveDeadLetterAsync(Endpoint("orders-sub"), 10, cancellation);

        var message = (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message;
        message.Should().StartWith("Dead-letter topic 'orders-dead' has several subscriptions: ");
        message.Should().Contain("dead-reader-a").And.Contain("dead-reader-b");
        message.Should().EndWith("Receive from the one you want directly.");

        // Nothing was taken from either consumer.
        (await connection.ReceiveAsync(Endpoint("dead-reader-a"), 10, cancellation)).Should().ContainSingle();
        (await connection.ReceiveAsync(Endpoint("dead-reader-b"), 10, cancellation)).Should().ContainSingle();
    }

    [Fact(DisplayName = "Inspecting a subscription names its topic and dead-letter topic", Timeout = 120000)]
    public async Task Inspect_a_subscription()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await CreateTopicAsync("orders");
        await CreateTopicAsync("orders-dead");
        await CreateSubscriptionAsync("orders-sub", "orders", deadLetterTopic: "orders-dead");
        var connection = await ConnectAsync(cancellation);

        var properties = (await connection.InspectAsync("orders-sub", "Subscription", cancellation)).Properties
            .ToDictionary(p => p.Key, p => p.Value);

        properties["Topic"].Should().Be("orders");
        properties["Delivery type"].Should().Be("Pull");
        properties["Dead-letter topic"].Should().Be("orders-dead");
        properties["Max delivery attempts"].Should().Be("5");
    }

    [Fact(DisplayName = "Inspecting a topic lists its subscriptions", Timeout = 120000)]
    public async Task Inspect_a_topic()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await CreateTopicAsync("orders");
        await CreateSubscriptionAsync("billing", "orders");
        await CreateSubscriptionAsync("shipping", "orders");
        var connection = await ConnectAsync(cancellation);

        var subscriptions = (await connection.InspectAsync("orders", "Topic", cancellation)).Properties
            .Single(p => p.Key == "Subscriptions").Value;

        subscriptions.Should().Contain("billing").And.Contain("shipping");
    }
}
