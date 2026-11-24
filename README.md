# LogistFlow

LogistFlow is a logistics case study developed one module at a time in this repository.

## Module 02 — Refactoring, REST APIs and Testing

LogistFlow is a local .NET 10 console application. The menu registers collections, lists and filters them by status or priority, updates their status by ID, and summarizes package totals and collection counts. New collections start as `Pending`.

This module adds postal address lookup, asynchronous JSON storage, and xUnit tests to the [Module 01 foundation](https://github.com/FilipeAlan/LogistFlow/tree/v1.0.0). Existing records keep their IDs and free-text addresses. The application uses the same six JSON fields as Module 01.

### Registration and ViaCEP

Enter a Brazilian postal code (CEP) as eight digits or in `00000-000` format. The app validates it locally, then awaits an HTTPS GET to `https://viacep.com.br/ws/{cep}/json/`. The request contains only the postal code. A successful lookup fills in the available street, address complement, district, city, and state. You can supply missing fields and add a building number or other directions before saving. [ViaCEP service documentation](https://viacep.com.br/)

An invalid format prompts you to re-enter the CEP. An unknown CEP, HTTP error, network failure, five-second timeout, or unusable response opens manual address entry. Registration continues after the lookup outcome is known. The app saves the completed address and formatted CEP together.

### Project structure

| Location | Responsibility |
| --- | --- |
| `LogistFlow/Program.cs` and `Terminal/` | Open the local session, assemble the app, prompt the operator, and display results. |
| `LogistFlow/Application/` | Coordinate lookups and saves, track collections, loading, errors and active filters, and calculate the operational summary. |
| `LogistFlow/Domain/` | Define a collection, its priority and status, and the rules for valid records. |
| `LogistFlow/Integration/` | Call ViaCEP using `HttpClient` and interpret its responses. |
| `LogistFlow/Persistence/` | Read and validate local JSON asynchronously and replace the file after a successful write. |
| `LogistFlow.Tests/` | Exercise business rules, application state, storage and simulated ViaCEP responses. |

### Run

Open `LogistFlow.slnx` in Visual Studio with .NET 10 support and press **Ctrl+F5**. To run from a terminal, use the .NET 10 SDK from the repository root.

```sh
dotnet run --project LogistFlow/LogistFlow.csproj
```

### Build and test

Run from the repository root with the .NET 10 SDK. The first build restores the xUnit test packages. The unit tests simulate HTTP responses. The PowerShell smoke test runs the console against disposable files, routes lookups to an unavailable loopback proxy, and checks the manual fallback and persistence across restarts.

```powershell
dotnet build LogistFlow.slnx --configuration Release
dotnet test LogistFlow.slnx --configuration Release --no-build
./LogistFlow/smoke.ps1
```

See the [console guide](LogistFlow/README.md) for local storage and recovery.

## Planned distributed architecture

The architecture documents describe the event-driven platform planned for future modules, including delivery tracking, notifications, audit, and customer support. Module 02 runs locally with a JSON file. Services such as RabbitMQ and PostgreSQL belong to the distributed design.

We use Spec-Driven Development to review one architecture section at a time. Decisions remain **Proposed** until reviewed. The architecture document marks accepted decisions as **Approved**.

## Documentation index

- [Architecture](docs/ARCHITECTURE.md) — the ten architecture topics and the proposed technology stack.
- [Event catalog](docs/EVENT-CATALOG.md) — events defined in the specifications.
- [Glossary](docs/GLOSSARY.md) — logistics and architecture terminology.
- [Architecture decision records](docs/adr/) — proposed decisions and their review outcomes.
- [Specifications](docs/specs/) — specifications for incremental review.
