# SMT Order Manager

[![CI](https://github.com/mikhail-poda/smt-order-manager/actions/workflows/ci.yml/badge.svg)](https://github.com/mikhail-poda/smt-order-manager/actions/workflows/ci.yml)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![License](https://img.shields.io/badge/license-MIT-green)

A cross-platform .NET order management system for SMT (Surface-Mount Technology) production. It maintains the component library and board designs, manages production orders, and simulates downloading an order to a production line. It is modelled as one module of an SMT shop-floor software suite, at the start of the flow from product data to the production line. See the [system scope](docs/requirements.md#system-scope) for what is and isn't covered.

> **Status:** Phase 1 (console application) is complete. Phase 2 is planned. Implementation progress is tracked in [docs/requirements.md](docs/requirements.md).

## Goals

The project is developed in two phases.

**Phase 1: core application**

- Create, edit, search and remove one or more orders, boards and components
- Download an order to a simulated production line
- JSON serialization for interoperability
- JSON file persistence across application restarts
- Logging of actions and errors
- Unit tests for the core logic

**Phase 2: extensions**

- REST API and web UI with authentication
- SQLite as the default persistence, with JSON files remaining selectable via configuration
- Docker setup
- CI/CD pipeline with cloud deployment

The domain is modelled using domain-driven design (DDD). The persistence layer is kept behind abstractions so that adding SQLite in Phase 2 does not require changes to the domain model or application logic.

## Getting started

The only prerequisite is the [.NET 10 SDK](https://dotnet.microsoft.com/download). The application runs on Windows, Linux and macOS.

```bash
dotnet build
dotnet test
dotnet run --project src/SmtOrderManager.Cli
```

Run the commands from the repository root. The data, inbox and log folders are created relative to the working directory:

| Folder | Content |
|---|---|
| `data/` | The stored components, boards and orders (`components.json`, `boards.json`, `orders.json`) |
| `inbox/` | The jobs accepted by the simulated SMT line, one JSON file per download |
| `logs/` | The daily log files. The console shows warnings and errors only. |

### Demo

On a start with an empty `data/` folder, the application creates demo data: two components, three boards and two orders. To see both outcomes of a download, open **Orders → Download to SMT line**:

- **Controllers and sensors** is accepted. The job appears in `inbox/`, and the order can no longer be edited.
- **Backplanes** is rejected, because the backplane is longer than the simulated line allows. The order stays a draft.

Delete the `data/` folder to start over.

### Configuration

Settings are read from `appsettings.json` next to the executable and can be overridden on the command line (`--Section:Key=value`) or with environment variables (`Section__Key=value`). Invalid values stop the application at startup with a message naming the setting.

| Setting | Default | Meaning |
|---|---|---|
| `JsonStorage:DataDirectory` | `data` | Where the data files are stored |
| `SimulatedSmtLine:LineId` | `SMT-SIM-01` | The identifier the line reports |
| `SimulatedSmtLine:InboxDirectory` | `inbox` | Where accepted jobs are written |
| `SimulatedSmtLine:SupportedSchemaVersions` | `[1]` | The download payload versions the line accepts |
| `SimulatedSmtLine:MaxBoardLength` / `MaxBoardWidth` | `510` / `460` | The largest board the line accepts, in millimetres |
| `SimulatedSmtLine:IsAvailable` | `true` | `false` simulates an unreachable line |
| `DemoData:Enabled` | `true` | Whether demo data is created on a start with an empty store |

For example, to try a line outage without demo data:

```bash
dotnet run --project src/SmtOrderManager.Cli -- --SimulatedSmtLine:IsAvailable=false --DemoData:Enabled=false
```

### Project structure

| Project | Content |
|---|---|
| `src/SmtOrderManager.Domain` | Aggregates, value objects and the total component demand domain service |
| `src/SmtOrderManager.Application` | Use cases, repository and SMT line ports, and the versioned download contract |
| `src/SmtOrderManager.Infrastructure` | JSON file repositories and the simulated SMT line |
| `src/SmtOrderManager.Cli` | Console user interface and composition root |
| `tests/*` | One xUnit v3 test project each for Domain, Application and Infrastructure |

See the [architecture](docs/architecture.md) for the dependency rule between the projects and the class outline.

## Documentation

The documentation follows [arc42](https://arc42.org) in a lightweight form, covering only the chapters that carry information for a project of this size.

| Document | Content |
|---|---|
| [Requirements](docs/requirements.md) | Goals, interpretations, design decisions and implementation status |
| [Domain model](docs/domain-model.md) | Ubiquitous language (glossary), bounded context, aggregates, business rules and modelling rationale |
| [Architecture](docs/architecture.md) | Solution strategy, building blocks, class outline, integration contract and persistence evolution |
| [Testing](docs/testing.md) | Test strategy per layer, coverage by topic, contract tests and conventions |

## Tech stack

| Concern | Phase 1 | Phase 2 |
|---|---|---|
| Runtime | .NET 10 (LTS) | .NET 10 (LTS) |
| Host | Console application | ASP.NET Core Web API + web UI |
| Persistence | JSON files via `System.Text.Json` | SQLite (default), JSON files (selectable) |
| JSON | `System.Text.Json` | `System.Text.Json` |
| Logging | Serilog | Serilog |
| Testing | xUnit v3 | xUnit v3 |
| Web | – | ASP.NET Core Web API, React + TypeScript |
| Containers | – | Docker, docker-compose (SQLite on a persistent volume) |

## License

[MIT](LICENSE)