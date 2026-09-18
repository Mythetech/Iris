# Iris

A desktop testing tool for distributed applications. Think Postman, but for message brokers.

## Overview

Iris lets developers visually connect to message brokers, send and receive messages, and debug event flows, without writing boilerplate test code.

### Supported Message Brokers
- **RabbitMQ**: queues, exchanges, and routing
- **Azure Service Bus**: queues and topics
- **Azure Storage Queues**
- **AWS SQS**

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
| Infrastructure | [Mythetech Framework](https://github.com/Mythetech) (message bus, settings, desktop services) |
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
│   └── Iris.Templates/        # Reusable message templates
├── Samples/                   # Sample apps used to exercise Iris end to end
├── Iris.Components.Test/      # bUnit component tests
├── Iris.Desktop.Test/         # Desktop host and pipeline tests
├── Iris.Integration.Tests/    # Testcontainers integration tests
└── docs/                      # Architecture documentation
```

## Prerequisites

- [.NET 11 SDK](https://dotnet.microsoft.com/) - the exact prerelease build is pinned in `global.json`, so `dotnet restore` will tell you if yours is older
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
# All tests
dotnet test

# Component tests only (no Docker required)
dotnet test Iris.Components.Test

# Integration tests (requires Docker for Testcontainers)
dotnet test Iris.Integration.Tests
```

## Key Capabilities

- **Broker Connections**: connect to local or cloud broker instances
- **Message Publishing**: send messages to queues and topics with framework wrapping
- **Message Reading**: peek queues non-destructively, or receive messages off them, including dead-letter sub-queues
- **Auto-Discovery**: automatically detects local RabbitMQ and Azure Storage Emulator
- **Message History**: persists sent/received messages locally via LiteDB
- **Templates**: save and reuse common message patterns
- **Dynamic Type Loading**: load assemblies to use your own message contracts
- **Settings**: configurable via Mythetech Framework settings panel with local persistence

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes
4. Push to the branch
5. Open a Pull Request

## License

MIT. See [LICENSE](LICENSE) for details.
