# Connection Providers

Third party connections are essential for Iris to integrate with to ensure a seamless messaging experience. Each connection provider offers a way to interact with a component/s of distributed systems across a diverse breadth of technologies and hosting architectures. 

Iris strives to achieve high compatability with services across local and cloud environments, aiming to be the ubiquitious messaging companion tool.

## Azure

Azure offers several message or event based services that have become widely populalr.

### Azure Service Bus (Supported)

Azure Service Bus is a fundamental enterprise message bus for distributed applications.


### Azure Queue Storage (Preview, emulator & cloud)

Azure Queue Storage offers a simpler way to get started for simpler messaging scenarios to decouple processing workloads.

### Azure Event Grid (Future)


## Amazon Web Services (AWS)

Amazon offers their own sets of solutions we look to integrate with.

### Simple Queue Service (SQS) (Supported)

## RabbitMQ

A popular open source transport that can run hosted or out of docker.

### Docker (Preview)

### CloudAMPQ (Suppoted)

## Send capabilities

| Connection | `IHeaderCarrier` | `ITransportPropertyCarrier` |
|---|---|---|
| RabbitMQ | `int.MaxValue`, any key, all four data types (management API `properties.headers` takes JSON strings, numbers, booleans; timestamps sent as ISO 8601 strings) | all six |
| Azure Service Bus | `int.MaxValue`, any key, all four (`ApplicationProperties` accepts string, int, bool, `DateTimeOffset`) | `MessageId`, `CorrelationId`, `ContentType` (`Subject` is the AMQP subject field, not the `type` property, so `Type` is not carried; EasyNetQ and Wolverine are unsupported here) |
| Amazon SQS | 10, `^[A-Za-z0-9_.-]+$`, `String` and `Integer` (attribute `DataType` String / Number) | not implemented |
| Azure Queue Storage | not implemented | not implemented |
| Emulated (tests) | `int.MaxValue`, any key, all four; records what it was handed | all six; records what it was handed |

| Adapter | Body | Header (required in bold) | TransportProperty (required in bold) |
|---|---|---|---|
| MassTransit | `messageType`, `messageId`, `correlationId`, `conversationId`, `sourceAddress`, `sentTime`, `host` | | |
| NServiceBus | `Id`, `Headers`, `Body`, `CorrelationId`, `MessageIntent`, `ReplyToAddress` | | |
| Rebus | | **`rbs2-msg-id`**, **`rbs2-msg-type`**, **`rbs2-content-type`**, `rbs2-corr-id`, `rbs2-corr-seq`, `rbs2-senttime` (String; Rebus parses its own `O` format), `rbs2-return-address`, `rbs2-sender-address`, `rbs2-intent` | |
| Brighter | | **`MessageType`**, `MessageId`, `Topic`, `HandledCount` (Integer), `CorrelationId`, `cloudEvents_id`, `cloudEvents_specversion`, `cloudEvents_type`, `cloudEvents_source`, `cloudEvents_time` (Timestamp) | `MessageId`, `ContentType`, `Type` (Brighter reads the body content type from the AMQP `type` property, so the adapter sets it to `application/json`), `Timestamp` |
| Wolverine | | `wolverine-protocol-version`, `source`, `sent-at` (String; Wolverine parses `yyyy-MM-dd HH:mm:ss:ffffff Z`), `conversation-id` | **`Type`** (the full type name, or the `[MessageIdentity]` alias), `ContentType`, `MessageId`, `CorrelationId` |
| EasyNetQ | | | **`Type`** (`Namespace.Type, Assembly` for EasyNetQ 8's `DefaultTypeNameSerializer`; `Namespace.Type:Assembly` when the `EasyNetQTypeNameFormat` input is `Legacy`, for EasyNetQ 7), `ContentType`, `MessageId`, `CorrelationId`, `Timestamp`, `Persistent` |

`FrameworkCompatibility.Check` in `Iris.Brokers` matches a framework's keys against a connection's carriers; the UI reads the result through `IFrameworkCatalog` and the send path enforces it again in `LocalConnectionManager`.