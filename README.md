# SMT Order Manager

![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![License](https://img.shields.io/badge/license-MIT-green)

A cross-platform .NET order management system for SMT (Surface-Mount Technology) production. It maintains the component library and board designs, manages production orders, and simulates downloading an order to a production line. It is modelled as one module of an SMT shop-floor software suite, at the start of the flow from product data to the production line. See the [system scope](docs/requirements.md#system-scope) for what is and isn't covered.

> **Status:** early development. Implementation progress is tracked in [docs/requirements.md](docs/requirements.md).

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

## Documentation

The documentation follows [arc42](https://arc42.org) in a lightweight form, covering only the chapters that carry information for a project of this size.

| Document | Content |
|---|---|
| [Requirements](docs/requirements.md) | Goals, interpretations, design decisions and implementation status |
| [Domain model](docs/domain-model.md) | Ubiquitous language (glossary), bounded context, aggregates, business rules and modelling rationale |
| [Architecture](docs/architecture.md) | Solution strategy, building blocks and persistence evolution |

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