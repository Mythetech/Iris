# Contributing to Iris

Thank you for your interest in contributing to Iris! This document provides guidelines and instructions for contributing.

## 🚀 Getting Started

### Prerequisites

- **.NET 9 SDK** (9.0.100 or later)
- **Visual Studio 2022** or **JetBrains Rider** (recommended)
- **Git** for version control

### Development Setup

1. **Fork and clone the repository**
   ```bash
   git clone https://github.com/Mythetech/Iris.git
   cd Iris
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Build the solution**
   ```bash
   dotnet build
   ```

4. **Run the desktop application**
   ```bash
   cd Iris.Desktop
   dotnet run
   ```

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test projects
dotnet test Iris.Components.Test
dotnet test Iris.Integration.Tests
```

## 📁 Project Structure

| Project | Description |
|---------|-------------|
| `Iris.Desktop` | Main Photino.Blazor desktop application |
| `Iris.Components` | Shared Blazor UI components |
| `Iris.Brokers` | Message broker abstractions and connectors |
| `Iris.Contracts` | Shared DTOs, commands, and events |
| `Services/` | Backend services (Assemblies, History, Templates) |

## 🔧 Development Guidelines

### Code Style

- Follow the conventions in `.editorconfig`
- Use `_` prefix for private fields (e.g., `_connectionManager`)
- Avoid inline comments describing obvious code; prefer well-named methods
- Use `ILogger<T>` instead of `Console.WriteLine` for logging

### Architecture Patterns

Iris follows these architectural patterns (see `/docs/Architecture/`):

- **CQRS** - Command Query Responsibility Segregation
- **DDD** - Domain-Driven Design
- **REPR** - Request-Endpoint-Response Pattern

### Adding New Broker Support

1. Create a new folder under `Iris.Brokers/` (e.g., `Iris.Brokers/Kafka/`)
2. Implement `IConnector` interface for connection factory
3. Implement `IConnection` interface for the connection
4. Register the connector in DI (auto-discovered via assembly scanning)

### Adding New UI Components

1. Add components to `Iris.Components/` in the appropriate folder
2. Follow the `IrisBaseComponent` pattern for shared functionality
3. Write tests in `Iris.Components.Test/`

## 🧪 Testing

### Unit Tests
- Use **xUnit** as the test framework
- Use **NSubstitute** for mocking
- Use **FluentAssertions** for readable assertions
- Use **bUnit** for Blazor component testing

### Integration Tests
- Integration tests use **Testcontainers** for real broker instances
- These tests may require Docker to be running

### Test Naming Convention
```csharp
[Fact(DisplayName = "Can connect to docker RabbitMq")]
public async Task Can_Connect_ToRabbit()
```

## 📝 Pull Request Process

1. **Create a feature branch** from `main`
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Make your changes** following the guidelines above

3. **Write/update tests** for your changes

4. **Ensure all tests pass**
   ```bash
   dotnet test
   ```

5. **Commit with descriptive messages**
   ```bash
   git commit -m "Add support for Kafka broker connections"
   ```

6. **Push and create a Pull Request**

### PR Checklist

- [ ] Code follows project conventions
- [ ] Tests added/updated and passing
- [ ] No `Console.WriteLine` in production code (use `ILogger`)
- [ ] No hardcoded secrets or connection strings
- [ ] Documentation updated if needed

## 🐛 Reporting Issues

When reporting issues, please include:

- Iris version
- Operating system
- Steps to reproduce
- Expected vs actual behavior
- Relevant logs or screenshots

## 💡 Feature Requests

Feature requests are welcome! Please:

1. Check existing issues first
2. Describe the use case
3. Explain why this would benefit other users

## 📄 License

By contributing, you agree that your contributions will be licensed under the MIT License.

---

Thank you for contributing to Iris! 🎉

