# Iris

A desktop testing tool for distributed applications. Think Postman, but for message brokers.

## Overview

Iris lets developers visually connect to message brokers, send and receive messages, and debug event flows, without writing boilerplate test code.

### Supported Message Brokers
- **RabbitMQ**: queues, exchanges, and routing
- **Azure Service Bus**: queues and topics
- **Azure Storage Queues**
- **AWS SQS**
- **Google Cloud Pub/Sub**: topics and subscriptions

### Framework Adapters
Messages can be wrapped in framework-specific envelopes:
- **MassTransit**
- **NServiceBus**
- **EasyNetQ**
- **Wolverine**
- **Rebus**
- **Brighter**

## Tech Stack

| Layer | Technology |
|-------|------------|
| Runtime | .NET 11, C# 14 |
| Desktop | [Hermes](https://github.com/Mythetech/Hermes) (cross-platform native window + WebView) |
| UI | Blazor + [MudBlazor](https://mudblazor.com/) |
| Infrastructure | [Mythetech Framework](https://github.com/Mythetech/Mythetech.Framework) (message bus, settings, desktop services) |
| Local Storage | [LiteDB](https://www.litedb.org/) (embedded NoSQL) |
| Distribution | Velopack |

## Project Structure

```
Iris/
├── Iris.Desktop/              # Main desktop application entry point
├── Iris.Components/           # Shared Blazor UI components
├── Iris.Contracts/            # Shared DTOs and interfaces
├── Iris.Contracts.Generators/ # Source generator for the audit action lookup
├── Iris.Brokers/              # Message broker connectors
│   └── Frameworks/            # Framework envelope adapters
├── Services/
│   ├── Assemblies/            # Dynamic assembly loading for message types
│   ├── History/               # Message history records
│   ├── Sagas/                 # Saga discovery and state machine graphs
│   ├── Telemetry/             # OTLP span ingestion
│   ├── Iris.Templates/        # Empty placeholder; template code lives in Iris.Components and Iris.Desktop
│   └── Iris.Brokers.Test/     # Unit tests for Iris.Brokers
├── Samples/                   # Sample apps used to exercise Iris end to end
├── Iris.Components.Test/      # bUnit component tests
├── Iris.Desktop.Test/         # Desktop host and pipeline tests
├── Iris.Integration.Tests/    # Testcontainers integration tests
└── docs/                      # Architecture documentation
```

`Assemblies/`, `Sagas/` and `Telemetry/` each hold the service project and a sibling test project (`Iris.Assemblies.Test`, `Iris.Sagas.Test`, `Iris.Telemetry.Test`).

## Prerequisites

- [.NET 11 SDK](https://dotnet.microsoft.com/) - the exact build is pinned in `global.json`, so `dotnet restore` will tell you if yours is older
- **Message Brokers** (optional): RabbitMQ, Azure Service Bus, etc. for testing

## Getting Started

```bash
git clone https://github.com/Mythetech/Iris.git
cd Iris
dotnet restore
dotnet build
dotnet run --project Iris.Desktop
```

## Testing

```bash
# All tests (includes the integration tests, so Docker must be running)
dotnet test

# Component tests only (no Docker required)
dotnet test Iris.Components.Test

# Integration tests (requires Docker for Testcontainers)
dotnet test Iris.Integration.Tests
```

## Key Capabilities

- **Broker Connections**: connect to local or cloud broker instances
- **AWS Authentication**: connect to SQS with access keys, a named AWS profile, or the default credential chain
- **Message Publishing**: send messages to queues and topics with framework wrapping
- **Message Reading**: peek queues non-destructively, or receive messages off them, including dead-letter sub-queues
- **Auto-Discovery**: at startup, connects to a local RabbitMQ (default guest account) and the Azure Storage emulator when they are running
- **Endpoint Browsing**: list and search queues, topics, exchanges and subscriptions across your connections, inspect their properties, and jump straight to sending or reading
- **Message History**: persists sent/received messages locally via LiteDB
- **Templates**: save and reuse common message patterns
- **Dynamic Type Loading**: load assemblies on the Packages page to use your own message contracts, browse their types and properties, and generate a sample message body from a type
- **Sagas**: discover MassTransit state machines in loaded assemblies, view each one as a state graph, and follow live saga instances through their transitions
- **Telemetry**: turn on a local OTLP/HTTP receiver, point your app's OpenTelemetry exporter at it, and inspect the spans Iris receives
- **Home Dashboard**: see your connections and their endpoints at a glance, charted by connection and endpoint type
- **Command Palette and Shortcuts**: jump to any page or to settings with Cmd/Ctrl+K, and list the keyboard shortcuts with Cmd/Ctrl+?
- **In-App Updates**: checks for a new version at launch and shows an indicator in the title bar to download it and restart into it
- **Settings**: configurable via Mythetech Framework settings panel with local persistence

## Documentation

- [Product page](https://www.mythetech.com/iris)
- [Changelog](CHANGELOG.md)
- Architecture: [CQRS](docs/Architecture/CQRS.md), [DDD](docs/Architecture/DDD.md), [REPR](docs/Architecture/REPR.md)
- [Connection providers](docs/Brokers/Providers.md)
- [State management](docs/Components/StateManagement.md)
- [Templates service](docs/Services/Templates.md)
- [MassTransit saga sample](Samples/Iris.Samples.MassTransitSaga/README.md): a walkthrough of the Sagas page and the OTLP receiver

## Contributing

Contributions are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) for setup, development guidelines and the pull request process. To report a vulnerability, follow [SECURITY.md](SECURITY.md).

## License

MIT. See [LICENSE](LICENSE) for details.
