# ArchitectLuna

ArchitectLuna is a CLI code generator for .NET APIs — think "Intent Architect Lite." A small
YAML **Intent Model** (`.architect/model.yaml`) describes a solution's features and entities;
`architect-luna generate` turns that into a working API: commands, handlers, validators, and
minimal-API endpoints — with real persistence wired in if you ask for it.

It supports two interchangeable backend adapters — **MediatR** and **Wolverine** — that produce
different implementations from the *same* model, four interchangeable persistence providers —
**In-Memory**, **EF Core / Postgres**, **EF Core / SQL Server**, and **Marten** — that plug real
CRUD into generated handlers, and two interchangeable solution layouts — **Clean Architecture**
(the default: Api/Application/Domain/Infrastructure as four real projects, dependency rule pointing
inward — Request/Response DTOs live in a `Contracts/` subfolder of each Application feature slice,
not a separate project) and **vertical slice** (one Api project, features live inside it). Switching
`--adapter`
changes how a request is dispatched (MediatR's `ISender` vs Wolverine's `IMessageBus`); switching
`--persistence` changes how a handler talks to storage; switching `--architecture` changes which
projects the generated files land in. None of the three ever changes the route shape or HTTP
surface, because every adapter renders endpoints from the same shared templates regardless of
where they end up on disk.

**Handlers ship with real, working CRUD logic by default** — `--persistence in-memory` (the
default for `new api`) needs no database, no connection string, and no extra NuGet package: a
freshly scaffolded solution builds and runs immediately, and every generated endpoint does real
Create/Read/Update/Delete against a process-lifetime store instead of throwing
`NotImplementedException`. Swap in `efcore-postgres`, `efcore-sqlserver`, or `marten` when you need
data to survive a restart — same generated shapes, same handler call sites, just a different
storage backend wired in.

Every scaffolded solution — either layout — compiles and runs immediately with the full
**production foundation**: a Result pattern (`Result`/`Result<T>`/`Error`/`ValidationError`/
`PagedResult<T>`) that handlers return and endpoints translate to consistent HTTP status codes, a
`BaseEntity` every generated entity inherits, `IUserContext` + `IDateTimeProvider` abstractions
(HTTP-backed implementations provided), correlation-ID + exception-handling middleware, Serilog
request logging, Swagger, health checks (`/health`), a mapping layer (Request/Response DTOs with
explicit extension-method mappings), a Dockerfile + docker-compose.yml,
`launchSettings.json`/`appsettings.json` split by environment, generated docs and `.editorconfig`,
and xUnit test projects already wired up. Startup stays clean — `Program.cs` is just
`UseApiLogging` + `AddApi`/`AddApplication`/`AddInfrastructure` + `UseApiMiddleware`/`MapApiEndpoints`;
every registration detail lives behind those generated extension methods, and adding a feature
never edits `Program.cs`. There's nothing to bolt on by hand before the first `generate` run.

## The core idea: entity outwards

An **entity** is the source of truth for a feature's domain data. Everything downstream —
commands, queries, handlers, validators, endpoints, and (if persistence is configured) the actual
storage class — is generated *outward* from the entity, not hand-authored independently of it:

```
entity ──▶ commands (Create/Update/Delete) ──▶ handlers (real CRUD, unless --persistence none) ──▶ validators ──▶ endpoints
       └─▶ queries (GetById/GetAll)        ──▶ handlers (real reads,  unless --persistence none)                └─▶ endpoints
```

One `add entity` call gives you a full Create/Read/Update/Delete/List slice. You can still hand-add
bespoke commands/queries beyond CRUD with `add command` / `add query` when you need something an
entity's standard shape doesn't cover.

## Installing

`ArchitectLuna.Cli.csproj` is already set up as a [.NET tool](https://learn.microsoft.com/dotnet/core/tools/global-tools)
(`PackAsTool`, `ToolCommandName` = `architect-luna`, `PackageId` = `architect-luna`). Packaging it
and installing it globally makes `architect-luna` a normal command on your `PATH`, runnable from
**any** project folder — not just this repo.

### 0. Install from the team feed (no checkout needed)

Every merge to `master` automatically publishes the tool to the team's Azure Artifacts feed (see
"Publishing" below), so the fastest path is to install straight from there:

```bash
dotnet tool install --global architect-luna \
  --add-source https://pkgs.dev.azure.com/LoadshednomoConsulting/_packaging/Nuget-Packages/nuget/v3/index.json
```

The feed is private, so your machine needs Azure Artifacts credentials — either the
[Azure Artifacts Credential Provider](https://github.com/microsoft/artifacts-credprovider) (it
prompts for a device login on first restore) or a `nuget.config` entry with a PAT. Registering the
feed once makes updates a one-liner:

```xml
<add key="Nuget-Packages" value="https://pkgs.dev.azure.com/LoadshednomoConsulting/_packaging/Nuget-Packages/nuget/v3/index.json" />
```

```bash
dotnet tool update --global architect-luna
```

The steps below are the local, feed-free path — useful offline or when testing unmerged changes.

### 1. Package it as a NuGet tool (`dotnet pack`)

```bash
# From the repo root
dotnet pack src/ArchitectLuna.Cli/ArchitectLuna.Cli.csproj -c Release -o ./nupkg
```

This produces `./nupkg/architect-luna.<version>.nupkg` — a self-contained .NET tool package (the
version comes from `<Version>` in `ArchitectLuna.Cli.csproj`, currently `0.1.0`). Nothing here
needs network access beyond restoring the CLI's own build-time dependencies (Spectre.Console.Cli);
no separate publish step is required to produce the package.

### 2. Install it globally

**From the local folder you just packed** — no NuGet feed involved, works entirely offline once
`dotnet pack` has run:

```bash
dotnet tool install --global --add-source ./nupkg architect-luna
```

**From a published feed** — the team feed (step 0 above) is published automatically on every merge
to `master` (see step 4), so this is rarely needed by hand:

```bash
dotnet tool install --global architect-luna --add-source <your-feed-url>
# or, if the feed is already registered via `dotnet nuget add source` / nuget.config:
dotnet tool install --global architect-luna
```

Either way, `dotnet tool install --global` puts the command on `~/.dotnet/tools`, which needs to be
on your shell's `PATH` (the installer prints the one-liner to add it if it isn't already — usually
`export PATH="$PATH:$HOME/.dotnet/tools"` in your shell profile).

### 3. Run it on any project folder

Once installed, `architect-luna` behaves like any other global CLI — no relative paths, no `dotnet
run --project ...`, no reference to this repo's checkout at all:

```bash
cd ~/anywhere/you/want/a/new/api        # or an existing folder for `add feature`/`add entity`/`generate`
architect-luna new api MyApp
cd MyApp
architect-luna add feature Orders
architect-luna add entity Orders Order --field CustomerId:Guid --field Total:decimal
architect-luna generate
dotnet build && dotnet run --project src/MyApp.Api
```

`new api` defaults to `--persistence in-memory`, so the `dotnet run` above serves a fully working
CRUD API immediately — no database to stand up first (see "The core idea" above).

### 4. Publishing — automatic on every merge to `master`

`.github/workflows/publish-nuget.yml` packs the tool and pushes it to the Azure Artifacts feed
(`Nuget-Packages` in the `LoadshednomoConsulting` org) on every push to `master`. Nobody runs
`dotnet nuget push` by hand:

- **Versioning is automatic**: each publish is `<VERSION_PREFIX>.<run number>` (e.g. `0.1.42`) —
  the workflow's run number is the build number, so versions increase monotonically and
  `dotnet tool update` always sees new merges as newer. Bump major/minor by editing
  `VERSION_PREFIX` at the top of the workflow; the `<Version>` in `ArchitectLuna.Cli.csproj` only
  applies to local `dotnet pack` runs.
- **Gated**: the publish job builds the solution and runs the fast test suite first (the full
  E2E matrix runs on the same push via `ci.yml`), and pushes with `--skip-duplicate` so re-runs
  are harmless.
- **One-time setup**: the repo needs an `AZURE_ARTIFACTS_PAT` Actions secret — an Azure DevOps PAT
  with *Packaging → Read & write* scope. Details in the comment at the top of the workflow file.

### Updating / uninstalling

- **Pick up code changes after pulling a newer version of this repo:**
  `dotnet tool update --global --add-source ./nupkg architect-luna` after re-running `dotnet pack`
  (bump `<Version>` in `ArchitectLuna.Cli.csproj` first if you want `dotnet tool update` to see it
  as newer — otherwise `dotnet tool uninstall --global architect-luna` then re-install is the
  reliable path for a same-version rebuild).
- **From a published feed:** `dotnet tool update --global architect-luna`.
- **Uninstall entirely:** `dotnet tool uninstall --global architect-luna`.
- **Check what's installed and where:** `dotnet tool list --global`.

## Quick start

```bash
# Scaffold a new API solution (creates the .sln, an ASP.NET Core minimal-API project,
# Docker/Swagger/health-checks/logging, a test project, and .architect/model.yaml).
# No flags needed for a working, database-free API:
architect-luna new api BillingService
cd BillingService

# Group related entities/commands/queries under a feature
architect-luna add feature Invoices

# One entity call synthesizes Create/Update/Delete + GetById/GetAll
architect-luna add entity Invoices Invoice \
  --field CustomerId:Guid \
  --field AmountCents:long \
  --field Currency:string \
  --rule "AmountCents:GreaterThan(0)" \
  --rule "Currency:MaximumLength(3)"

# Render the model to real C# files — handlers get real Save/Find/Remove CRUD against the
# generated in-memory store, not a placeholder throw
architect-luna generate

dotnet build
dotnet run --project src/BillingService.Api   # POST/GET/PUT/DELETE /api/invoices work immediately
```

`--adapter` is `mediatr` or `wolverine` (default `mediatr`). `--persistence` is `in-memory`
(default, zero setup), `none` (placeholder-only handlers), `efcore-postgres`, `efcore-sqlserver`,
or `marten` — pass one explicitly to opt into a durable backend, e.g.
`architect-luna new api BillingService --persistence efcore-postgres`. `--architecture` is
`clean-architecture` (default: Api/Application/Domain/Infrastructure, dependency rule pointing
inward — Application never references Infrastructure directly; the concrete DbContext implements
an interface Application owns) or `vertical-slice`.

```bash
# Same model, one project instead of four — every slice self-contained under Features/
architect-luna new api BillingService --adapter mediatr --persistence efcore-postgres --architecture vertical-slice
```

Run it locally, or with Docker (a `Dockerfile` and `docker-compose.yml` — including a Postgres/SQL
Server service when persistence is configured — are already in the scaffold):

```bash
dotnet run --project src/BillingService.Api    # http://localhost:5080, Swagger at /swagger, health at /health
# or
docker compose up --build
```

Need something outside standard CRUD? Add it directly — bespoke commands/queries don't require an
entity:

```bash
architect-luna add command Invoices VoidInvoice --field Id:Guid --kind update
architect-luna add query Invoices SearchInvoices --param CustomerId:Guid --param Status:string
```

Generation order is enforced with clear errors: nothing can be added outside a project
(`.architect/model.yaml` must exist), an entity's feature must exist first, entity-backed CRUD
requires the entity (`architect-luna add crud Invoices Invoice` re-synthesizes any missing standard
operations for an *existing* entity, and tells you to create it first otherwise), and duplicates
fail without touching the model.

## What `generate` produces

For an `Invoice` entity in an `Invoices` feature, `generate` renders (per adapter) one vertical
slice per command/query. Where those files land depends on `--architecture`:

- **clean-architecture** (default): the command/result/handler/validator/mappings under
  `src/{Solution}.Application/Features/Invoices/...`, the Request/Response DTOs under that same
  slice's `Contracts/` subfolder (`src/{Solution}.Application/Features/Invoices/.../Contracts/`),
  the endpoint under `src/{Solution}.Api/Features/Invoices/...`.
- **vertical-slice**: everything under `src/{Solution}.Api/Features/Invoices/...`.

| Operation | Route | Success | Files |
|---|---|---|---|
| Create | `POST /api/invoices` | `201 Created` + Location | Request, Command, Result, Response, Validator, Handler, Mappings, Endpoint |
| Update | `PUT /api/invoices/{id}` | `200 OK` | Request, Command, Result, Response, Validator, Handler, Mappings, Endpoint |
| Delete | `DELETE /api/invoices/{id}` | `204 No Content` | Command, Result, Handler, Endpoint |
| GetById | `GET /api/invoices/{id}` | `200 OK` | Query, Result, Response, Handler, Mappings, Endpoint |
| GetAll | `GET /api/invoices` | `200 OK` (list) | Query, Result (wrapped as `IReadOnlyList<T>`), Response, Handler, Mappings, Endpoint |

Handlers return `Result<T>` — not-found, conflict, and validation outcomes are values, not
exceptions — and every endpoint maps failures through one shared `ToProblem()` extension:
Validation→400, NotFound→404, Conflict→409, Unauthorized→401, Forbidden→403, anything else→500.
Mappings are explicit generated extension methods (`request.ToCommand()`, `result.ToResponse()`)
— no mapping library.

If `--persistence` is anything but `none`, `generate` additionally emits a domain/entity class per
entity (`Entities/Invoice.cs` for in-memory and EF Core, `Documents/Invoice.cs` for Marten) and one
solution-level file that every entity shares — `InMemoryStore.cs` for in-memory, a `DbContext` with
a `DbSet<T>` per entity for EF Core (Marten needs neither, since `IDocumentSession` covers every
document type generically). For vertical slice these live under `src/{Solution}.Api/Persistence/`;
for Clean Architecture, entity classes go to `src/{Solution}.Domain/` and the solution-level file
to `src/{Solution}.Infrastructure/`. Under Clean Architecture, Application never references
Infrastructure directly: for in-memory and EF Core, an `I{Solution}DbContext`/`IInMemoryStore`
interface lives in Application (`Persistence/...`) and the concrete class in Infrastructure
implements it — handlers depend on the interface, Program.cs (the Api project's composition root)
wires the concrete type to it. Handler bodies get the storage dependency injected — constructor
injection for MediatR, an extra static-method parameter for Wolverine (its own convention) — and
contain real Save/Find/Remove (in-memory), Add/Find/Remove/SaveChanges (EF Core), or
Store/Load/Delete/Query (Marten) calls instead of a placeholder `throw`.

Every endpoint implements a shared `IEndpointDefinition` interface; `Program.cs` discovers and
maps them by reflection, so it never needs to be regenerated as features grow.

Handler bodies are wrapped in `// <architect:region name="handler-body"> ... // </architect:region>`
markers. Re-running `generate` after you've filled in real logic preserves everything inside the
markers and regenerates everything else — so evolving the model (new fields, a renamed command)
never silently discards your hand-written business logic.

## The UI

`ArchitectLuna.Ui` is a small Razor Pages app for browsing and editing a model without the CLI:
point it at a solution root (the directory containing `.architect/model.yaml`) and it shows the
model's features/entities/commands/queries with their inferred routes, offers a form to add a new
entity (same validation/collision rules as `add entity`), and a button that shells out to the built
CLI to run `generate`. Run it with `dotnet run --project src/ArchitectLuna.Ui`.

## Repo layout

```
src/
  ArchitectLuna.Cli                  Spectre.Console.Cli commands: new, add, generate
  ArchitectLuna.Core                 Intent Model (IR), naming, route inference, validation,
                                      manifest, protected-region merging — no framework knowledge
  ArchitectLuna.Templates            Scriban .sbn templates (embedded resources) + engine
  ArchitectLuna.Adapters.MediatR     IFrameworkAdapter implementation for MediatR
  ArchitectLuna.Adapters.Wolverine   IFrameworkAdapter implementation for Wolverine
  ArchitectLuna.Persistence.InMemory IPersistenceGenerator for the zero-setup in-memory store
                                      (the `new api` default — no NuGet package, no database)
  ArchitectLuna.Persistence.EfCore   IPersistenceGenerator for EF Core (Postgres + SQL Server)
  ArchitectLuna.Persistence.Marten   IPersistenceGenerator for Marten (Postgres document DB)
  ArchitectLuna.Ui                   Razor Pages model viewer/editor, built directly on Core
tests/
  ArchitectLuna.Core.Tests           Fast xUnit unit tests, one folder per category: Naming,
                                      Routing, ModelValidation, CrudSynthesis, ProtectedRegions,
                                      Manifest, GenerationOrdering, YamlRoundTripping
  ArchitectLuna.Template.Tests       In-memory snapshot/structure tests over the GeneratedFile
                                      records (no file/process I/O): production foundation,
                                      Program.cs shape, slice file sets, layer placement and
                                      leak checks, status-code policy — both architecture profiles
  ArchitectLuna.EndToEnd.Tests       Slow xUnit tests that shell out to the real built CLI and run
                                      `dotnet build` on generated output — adapter x persistence
                                      combinations in both layouts, generation-ordering CLI error
                                      paths, protected-region regeneration, and a `dotnet test`
                                      run of one generated solution's own test suite
docs/requirements/                   The requirement documents this implementation tracks
.github/workflows/ci.yml             Fast-test job + dedicated E2E job on every push/PR, plus a
                                      scaffold/generate/build smoke matrix across every adapter x
                                      persistence x architecture combination (one representative
                                      cell also runs the generated solution's tests)
```

Targets **.NET 10**. Build with `dotnet build ArchitectLuna.sln`, test the fast suites with
`dotnet test tests/ArchitectLuna.Core.Tests` and `dotnet test tests/ArchitectLuna.Template.Tests`,
or everything (including the slow end-to-end suite) with `dotnet test ArchitectLuna.sln`.

## Contributing / working with AI agents

Every non-trivial change follows the structured workflow in `docs/workflow/feature-workflow.md`:
classify complexity (S/M/L), auto-plan complex work as a document in `docs/plans/`, implement
against the plan, and verify across the adapter × persistence matrix. `AGENTS.md` is the entry
point for any AI coding agent; in Claude Code, the `/feature` skill drives the whole workflow.
Recurring change types (new adapter, new persistence provider, CLI command, template change) have
step-by-step checklists in `docs/workflow/checklists/`.

## Status

M1–M4 are done: every adapter × persistence × architecture combination generates a compiling,
production-ready solution end to end, verified by actually scaffolding and building sample
projects (automated in `ArchitectLuna.EndToEnd.Tests`, not just eyeballed by hand). Entity-driven
CRUD synthesis, a zero-setup in-memory persistence provider (the `new api` default), real EF
Core/Marten persistence, Clean Architecture (now the default layout),
the full production foundation (Result pattern, BaseEntity, user-context/date-time abstractions,
correlation-ID + exception middleware, mapping layer, extension-method startup — see
`docs/requirements/001-implementation-architecture.md`), the three-tier test strategy
(Core unit / Template snapshot / EndToEnd — see `docs/requirements/002-testing-layer.md`), a CI
pipeline, and a model-editing UI are all implemented. See `docs/ROADMAP.md` for what's next
(`adapter switch`, EF Core migrations, additional adapters, publishing the packaged tool to a
real feed).
