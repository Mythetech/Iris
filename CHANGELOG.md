# Changelog

All notable changes to Iris will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Saga visualisation: discovery of MassTransit state machines from loaded assemblies, with
  a state graph and instance view
- OTLP telemetry ingestion so spans emitted by a system under test can be inspected in Iris
- Brighter adapter, bringing the framework adapters to six with parity across all of them
- Dead-letter support: peek and receive from dead-letter sub-queues on every broker that
  has them
- Command palette
- Connection and template persistence via LiteDB, restored on startup
- Framework send capabilities: adapters declare typed header keys and the selector disables
  transport/framework pairs it cannot satisfy, with a reason
- Open source community files (SECURITY.md, CHANGELOG.md, GitHub templates)
- GitHub Actions CI workflow

### Changed
- Desktop host moved from Photino to Hermes
- Targets .NET 11 on a pinned prerelease SDK
- Test stack moved to xUnit v3 on Microsoft.Testing.Platform
- Icons consolidated behind `IrisIcons`; every icon is a Material Symbols ligature
- Replaced example credentials in integration tests with placeholders

### Removed
- The IrisCloud-era contract surface, the unused `Navigation/` components and the dead
  members the repo review found

## [0.3.0] - 2025-01-11

### Added
- Material Symbols icon system replacing FontAwesome
- Improved ILogger usage across components

### Changed
- UI component cleanup and consistency improvements

## [0.2.0] - 2025-01-01

### Added
- Initial public release
- Desktop application using Photino.Blazor
- Support for multiple message brokers:
  - RabbitMQ
  - Azure Service Bus
  - Amazon SQS
  - Azure Storage Queues
- Message sending and receiving capabilities
- Connection management with auto-discovery
- Test history and templates
- MudBlazor-based UI components

## [0.1.0] - 2024-12-01

### Added
- Initial project structure
- Core broker abstractions
- Basic UI scaffolding
