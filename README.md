# SMT Order Manager

[![CI](https://github.com/mikhail-poda/smt-order-manager/actions/workflows/ci.yml/badge.svg)](https://github.com/mikhail-poda/smt-order-manager/actions/workflows/ci.yml)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![License](https://img.shields.io/badge/license-MIT-green)

A cross-platform .NET order management system for SMT (Surface-Mount Technology) production. It maintains the component library and board designs, manages production orders, and simulates downloading an order to a production line. It is modelled as one module of an SMT shop-floor software suite, at the start of the flow from product data to the production line. See the [system scope](docs/requirements.md#system-scope) for what is and isn't covered.

> **Status:** Phase 1 (console application) and Phase 2 (Web API, web UI, SQLite, Docker, CI) are complete. Implementation details are tracked in [docs/requirements.md](docs/requirements.md).

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

- REST API and web UI with login
- SQLite as the default persistence, with JSON files remaining selectable via configuration
- Docker setup
- CI pipeline that tests the code and publishes a container image for any container platform

The domain is modelled using domain-driven design (DDD). The persistence layer is kept behind abstractions, so adding SQLite in Phase 2 required no changes to the domain model or the use cases.

## Run with Docker

The quickest way to try the application. It needs only Docker.

**Build and run from the source**, from the repository root:

```bash
docker compose up --build
```

**Or run the published image** without cloning the repository. CI publishes it to the GitHub Container Registry for `amd64` and `arm64` on every push to `main`:

```bash
docker run --rm -p 8080:8080 \
  -v smt-data:/data \
  -e Auth__Username=demo \
  -e Auth__PasswordHash='AQAAAAIAAYagAAAAEO26S4jq4t4azZDGaO9eMdEwR64YtZJaUoJY3ssEeLoQU7b8mEHqnRL7RW0kD4nfQQ==' \
  ghcr.io/mikhail-poda/smt-order-manager:latest
```

If the repository is private, the image is private too; log in first with `docker login ghcr.io` and a GitHub token that can read packages.

Then open <http://localhost:8080> and log in as **`demo`** with the password **`smt-demo`**. All data lives in the `smt-data` volume and survives restarts; `docker compose down -v` (or `docker volume rm smt-data`) starts over with fresh demo data.

The container is configured only through environment variables, see [configuration](#configuration). It refuses to start without `Auth__Username` and `Auth__PasswordHash`, so no image runs with a built-in password.

## Run from source

Prerequisites: the [.NET 10 SDK](https://dotnet.microsoft.com/download), and [Node.js 22](https://nodejs.org) for the web UI. Everything runs on Windows, Linux and macOS. Run the commands from the repository root.

```bash
dotnet build
dotnet test
```

**Console application:**

```bash
dotnet run --project src/SmtOrderManager.Cli
```

**Web API with the web UI:** build the UI once into the API's `wwwroot`, then start the API and open <http://localhost:5080>. The Development settings provide the demo user (`demo` / `smt-demo`).

```bash
cd src/SmtOrderManager.Web
npm ci
npm run build
cd ../..
dotnet run --project src/SmtOrderManager.Api
```

To work on the UI with live reload, keep the API running and start `npm run dev` in `src/SmtOrderManager.Web`, then open <http://localhost:5173>. The Vite dev server forwards `/api` to the API. The OpenAPI description is at `/openapi/v1.json`, and [`SmtOrderManager.Api.http`](src/SmtOrderManager.Api/SmtOrderManager.Api.http) walks through the API with the HTTP client of Rider, Visual Studio or VS Code.

Both hosts create their folders relative to the working directory:

| Folder | Content |
|---|---|
| `data/` | The SQLite database `smt-order-manager.db` (or the JSON files, if selected), and the API's login keys in `data/keys/` |
| `inbox/` | The jobs accepted by the simulated SMT line, one JSON file per download |
| `logs/` | The daily log files of the console application. Its console shows warnings and errors only; the API logs to the console. |

### Demo

On a start with an empty store, both hosts create demo data: two components, three boards and two orders. To see both outcomes of a download, use **Orders → Download** in the web UI or **Orders → Download to SMT line** in the console application:

- **Controllers and sensors** is accepted. The job appears in `inbox/`, and the order can no longer be edited.
- **Backplanes** is rejected, because the backplane is longer than the simulated line allows. The order stays a draft.

Delete the `data/` folder to start over.

### Configuration

Settings are read from `appsettings.json` next to the executable and can be overridden on the command line (`--Section:Key=value`) or with environment variables (`Section__Key=value`). Invalid values stop the application at startup with a message naming the setting.

| Setting | Default | Meaning |
|---|---|---|
| `Persistence:Provider` | `Sqlite` | `Sqlite` or `Json` |
| `Persistence:Sqlite:DatabasePath` | `data/smt-order-manager.db` | The SQLite database file |
| `JsonStorage:DataDirectory` | `data` | Where the JSON files are stored, if selected |
| `SimulatedSmtLine:LineId` | `SMT-SIM-01` | The identifier the line reports |
| `SimulatedSmtLine:InboxDirectory` | `inbox` | Where accepted jobs are written |
| `SimulatedSmtLine:SupportedSchemaVersions` | `[1]` | The download payload versions the line accepts |
| `SimulatedSmtLine:MaxBoardLength` / `MaxBoardWidth` | `510` / `460` | The largest board the line accepts, in millimetres |
| `SimulatedSmtLine:IsAvailable` | `true` | `false` simulates an unreachable line |
| `DemoData:Enabled` | `true` | Whether demo data is created on a start with an empty store |
| `Auth:Username` | (none) | API only: the user who can log in |
| `Auth:PasswordHash` | (none) | API only: the hash of that user's password, see below |
| `Auth:KeysDirectory` | `data/keys` | API only: where the keys that encrypt the login cookie are kept |

To create the hash for another password:

```bash
dotnet run --project src/SmtOrderManager.Api -- hash-password <password>
```

For example, to try a line outage with the JSON files and without demo data:

```bash
dotnet run --project src/SmtOrderManager.Cli -- --Persistence:Provider=Json --SimulatedSmtLine:IsAvailable=false --DemoData:Enabled=false
```

### Project structure

| Project | Content |
|---|---|
| `src/SmtOrderManager.Domain` | Aggregates, value objects and the total component demand domain service |
| `src/SmtOrderManager.Application` | Use cases, repository and SMT line ports, the versioned download contract and the demo data |
| `src/SmtOrderManager.Infrastructure` | SQLite and JSON file repositories, and the simulated SMT line |
| `src/SmtOrderManager.Cli` | Console user interface and its composition root |
| `src/SmtOrderManager.Api` | Web API with cookie login, serving the web UI; composition root of the web host |
| `src/SmtOrderManager.Web` | Web UI: React and TypeScript, built with Vite |
| `tests/*` | One xUnit v3 test project each for Domain, Application, Infrastructure and Api |

See the [architecture](docs/architecture.md) for the dependency rule between the projects and the class outline.

## Documentation

The documentation follows [arc42](https://arc42.org) in a lightweight form, covering only the chapters that carry information for a project of this size.

| Document | Content |
|---|---|
| [Requirements](docs/requirements.md) | Goals, interpretations, design decisions and implementation status |
| [Domain model](docs/domain-model.md) | Ubiquitous language (glossary), bounded context, aggregates, business rules and modelling rationale |
| [Architecture](docs/architecture.md) | Solution strategy, building blocks, class outline, integration contract, Web API, persistence and deployment |
| [Testing](docs/testing.md) | Test strategy per layer, coverage by topic, contract tests and conventions |
| [Download contract](docs/download-contract.md) | The fields, formats and consumer expectations of the order download payload |

## Tech stack

| Concern | Technology |
|---|---|
| Runtime | .NET 10 (LTS) |
| Hosts | Console application; ASP.NET Core Web API (minimal APIs) |
| Web UI | React 19, TypeScript, Vite |
| Persistence | SQLite via Entity Framework Core (default); JSON files via `System.Text.Json` (selectable) |
| JSON | `System.Text.Json` |
| Authentication | Cookie login for one configured user, ASP.NET Core Identity password hashing |
| Logging | Serilog |
| Testing | xUnit v3, ASP.NET Core `WebApplicationFactory` |
| Containers | Docker, docker-compose (SQLite on a persistent volume) |
| CI | GitHub Actions; images published to the GitHub Container Registry |

## License

[MIT](LICENSE)
