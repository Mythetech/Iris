# Changelog

All notable changes to Iris will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Open source community files (SECURITY.md, CHANGELOG.md, GitHub templates)
- GitHub Actions CI workflow

### Changed
- Replaced example credentials in integration tests with placeholders

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

[Unreleased]: https://github.com/Mythetech/Iris/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/Mythetech/Iris/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/Mythetech/Iris/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/Mythetech/Iris/releases/tag/v0.1.0
