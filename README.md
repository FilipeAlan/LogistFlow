# LogistFlow

LogistFlow is a logistics case study developed one module at a time in this repository.

## Module 01 — Foundations

The current implementation is a C# console application built with the .NET 10 standard libraries. Use the menu to register package collections, list or filter them by status or priority, update their status by ID, and view an operational summary.

New collections start as `Pending`. The application saves registrations and status changes to a local JSON file and reloads them at startup, preserving collection IDs across sessions.

### Run

Open `LogistFlow.slnx` in Visual Studio with .NET 10 support and press **Ctrl+F5**. To run from a terminal, use the .NET 10 SDK from the repository root.

```sh
dotnet run --project LogistFlow/LogistFlow.csproj
```

### Build and verify

Run the following commands in PowerShell from the repository root. The smoke tests exercise the menu, restart the application to check persistence, and use a temporary data directory.

```powershell
dotnet build LogistFlow.slnx --configuration Release
./LogistFlow/smoke.ps1
```

See the [Module 01 guide](LogistFlow/README.md) for details on local storage and recovery.

## Planned distributed architecture

The architecture documents describe the event-driven platform planned for future modules, including delivery tracking, notifications, audit, and customer support. Module 01 runs locally with a JSON file. Services such as RabbitMQ and PostgreSQL belong to the distributed design.

We use Spec-Driven Development to review one architecture section at a time. Decisions remain **Proposed** until reviewed. The architecture document marks accepted decisions as **Approved**.

## Documentation index

- [Architecture](docs/ARCHITECTURE.md) — the ten architecture topics and the proposed technology stack.
- [Event catalog](docs/EVENT-CATALOG.md) — events defined in the specifications.
- [Glossary](docs/GLOSSARY.md) — logistics and architecture terminology.
- [Architecture decision records](docs/adr/) — proposed decisions and their review outcomes.
- [Specifications](docs/specs/) — specifications for incremental review.
