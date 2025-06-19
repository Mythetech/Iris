# Iris

The first-of-its-kind testing platform for distributed applications. Think Postman, but for complex systems backed by message brokers.

## 🚀 Overview

Iris is a sophisticated desktop application that makes testing distributed applications easier and more intuitive. Built with modern .NET 9 and Photino.Blazor, it provides a powerful interface for testing complex systems that rely on message brokers, event-driven architectures, and microservices.

### The Problem
Today, testing distributed applications means **writing code** - lots of it. Developers spend hours writing boilerplate to:
- Connect to message brokers
- Publish test messages
- Listen for responses
- Validate message formats
- Debug event flows
- Set up test scenarios

**Iris eliminates this pain.** No more writing code just to test your distributed systems.

### What Makes Iris Special?
- **Message Broker Testing** - Test applications that use RabbitMQ, Azure Service Bus, AWS SQS, and more
- **Event-Driven Testing** - Validate event flows and message patterns in distributed systems
- **Visual Testing Interface** - Modern Blazor-based UI for creating and managing test scenarios
- **Local Development** - Run and test your distributed applications locally with ease
- **Template System** - Reusable test templates for common distributed system contracts
- **No Code Required** - Visual interface replaces hundreds of lines of test code

## 🏗️ Architecture

### Core Technologies
- **.NET 9** - Latest .NET framework with cutting-edge features
- **Blazor** - Modern web UI framework for .NET
- **Photino.Blazor** - Cross-platform desktop application framework
- **LiteDB** - Lightweight NoSQL database for test history and templates

### Supported Message Brokers
- **RabbitMQ** - Advanced message queuing and routing
- **Azure Service Bus** - Cloud-based messaging service
- **AWS SQS** - Simple queue service
- **NServiceBus** - Enterprise service bus
- **MassTransit** - Message bus abstraction
- **EasyNetQ** - RabbitMQ client library

### Architectural Patterns
- **Domain-Driven Design (DDD)** - Core business logic modeling for testing scenarios and sending messages
- **Command Query Responsibility Segregation (CQRS)** - Separation of test commands and queries
- **Repository Pattern** - Test history and template data access
- **Event-Driven Architecture** - Message bus for component communication

## 📁 Project Structure

```
Iris/
├── Iris.Desktop/                 # Main desktop application
├── Iris.Components/              # Shared UI components for testing interface
├── Iris.Contracts/               # Shared contracts and interfaces
├── Iris.Brokers/                 # Message broker implementations and connectors
├── Services/                     # Backend services
│   ├── Assemblies/              # Dynamic assembly loading for test scenarios and message preloading
│   ├── History/                 # Test execution history and results
│   └── Templates/               # Reusable test template management
├── Iris.Integration.Tests/       # Integration tests
├── Iris.Components.Test/         # Component tests
├── docs/                        # Architecture documentation
```

## 🛠️ Prerequisites

- **.NET 9 SDK** (9.0.100 or later)
- **Visual Studio 2022** or **JetBrains Rider** (recommended)
- **Git** for version control
- **Message Brokers** (optional) - RabbitMQ, Azure Service Bus, etc. for testing

## 🚀 Getting Started

### 1. Clone the Repository
```bash
git clone <repository-url>
cd Iris
```

### 2. Restore Dependencies
```bash
dotnet restore
```

### 3. Build the Solution
```bash
dotnet build
```

### 4. Run the Desktop Application
```bash
cd Iris.Desktop
dotnet run
```

## 🧪 Testing with Iris

### Key Testing Capabilities
- **Message Publishing** - Send messages to queues and topics
- **Message Consumption** - Listen to and validate incoming messages
- **Event Validation** - Verify event flows and message patterns
- **Load Testing** - Test system performance under various loads
- **Scenario Testing** - Create complex test scenarios involving multiple services
- **Template Management** - Save and reuse common testing patterns

### Example Use Cases
- Testing microservice communication patterns
- Validating event-driven architecture flows
- Load testing message broker performance
- Debugging distributed system issues
- Managing multiple local and cloud broker connections

## 🧪 Development Testing

### Run All Tests
```bash
dotnet test
```

### Run Specific Test Projects
```bash
dotnet test Iris.Integration.Tests
dotnet test Iris.Components.Test
```

The application uses Velopack for modern application packaging and distribution, providing automatic updates and cross-platform compatibility.

## 🏛️ Architecture Documentation

For detailed architecture information, see the documentation in the `docs/` directory:

## 🔧 Development

### Key Features
- **Cross-Platform Support** - Runs on Windows, macOS, and Linux
- **Modern UI** - Blazor-based interface with responsive design
- **Local Data Storage** - LiteDB for test history and template storage
- **Message Bus Integration** - Connect to various message brokers
- **Service Integration** - Modular service architecture for extensibility
- **Auto-Discovery** - Dynamic component and service discovery
- **Template System** - Reusable test scenarios and patterns

### Development Guidelines
- Follow Domain-Driven Design principles for test scenario modeling
- Use CQRS pattern for test command and query operations
- Implement comprehensive unit and integration tests
- Follow the established naming conventions
- Use the ubiquitous language defined in the domain
- Create reusable test templates for common distributed system patterns

## 📋 Dependencies

### Core Packages
- `Photino.Blazor` - Desktop application framework
- `LiteDB` - Local NoSQL database for test data
- `Velopack` - Application packaging
- `MudBlazor` - UI component library
- `Microsoft.Fast.Components.FluentUI` - Modern UI components

### Message Broker Support
- `MassTransit` - Message bus abstraction
- `NServiceBus` - Enterprise service bus
- `EasyNetQ` - RabbitMQ client
- `Azure.Messaging.ServiceBus` - Azure Service Bus client
- `AWSSDK.SQS` - AWS SQS client

### Testing
- `xUnit` - Testing framework
- `NSubstitute` - Mocking library
- `FluentAssertions` - Assertion library
- `bunit` - Blazor component testing

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📞 Support

For support and questions, please refer to the project documentation or create an issue in the repository.

---

**Built with ❤️ using .NET 9 and modern development practices**

*Iris - Making distributed application testing as easy as Postman made API testing*