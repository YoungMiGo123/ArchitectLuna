# ArchitectLuna

ArchitectLuna is a CLI code generator for .NET APIs — think "Intent Architect Lite." A small
YAML **Intent Model** (`.architect/model.yaml`) describes a solution's features and entities;
`architect-luna generate` turns that into a working vertical-slice API: commands, handlers,
validators, and minimal-API endpoints — with real persistence wired in if you ask for it.

It supports two interchangeable backend adapters — **MediatR** and **Wolverine** — that produce
different implementations from the *same* model, and four interchangeable persistence providers —
**In-Memory**, **EF Core / Postgres**, **EF Core / SQL Server**, and **Marten** — that plug real
CRUD into generated handlers. Switching `--adapter` changes how a request is dispatched (MediatR's
`ISender` vs Wolverine's `IMessageBus`); switching `--persistence` changes how a handler talks to
storage. Neither ever changes the route shape or HTTP surface, because every adapter renders
endpoints from the same shared templates.

**Handlers ship with real, working CRUD logic by default** — `--persistence in-memory` (the
default for `new api`) needs no database, no connection string, and no extra NuGet package: a
freshly scaffolded solution builds and runs immediately, and every generated endpoint does real
Create/Read/Update/Delete against a process-lifetime store instead of throwing
`NotImplementedException`. Swap in `efcore-postgres`, `efcore-sqlserver`, or `marten` when you need
data to survive a restart — same generated shapes, same handler call sites, just a different
storage backend wired in.

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

**From a published feed** (see step 3) — once the package is on GitHub Packages, NuGet.org, or any
other feed, anyone can install it without cloning this repo at all:

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

### 4. Publish to a real feed (optional — lets teammates/CI skip the checkout)

To make the package installable on *other* machines without them needing this repo checked out at
all, push it to a feed:

- **[GitHub Packages](https://docs.github.com/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry)**
  — natural fit since this repo is already on GitHub:
  ```bash
  dotnet nuget push ./nupkg/architect-luna.0.1.0.nupkg \
    --source https://nuget.pkg.github.com/YoungMiGo123/index.json \
    --api-key <a GitHub PAT with write:packages scope>
  ```
- **[NuGet.org](https://www.nuget.org/)** — for public distribution:
  ```bash
  dotnet nuget push ./nupkg/architect-luna.0.1.0.nupkg \
    --source https://api.nuget.org/v3/index.json \
    --api-key <your NuGet.org API key>
  ```

After that, anyone with the feed configured (`dotnet nuget add source ...` or a `nuget.config`)
just runs `dotnet tool install --global architect-luna` — no `git clone`, no local `dotnet pack`.

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
# and .architect/model.yaml). No flags needed for a working, database-free API:
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
`architect-luna new api BillingService --persistence efcore-postgres`.

Need something outside standard CRUD? Add it directly:

```bash
architect-luna add command Invoices VoidInvoice --field Id:Guid --kind update
architect-luna add query Invoices SearchInvoices --param CustomerId:Guid --param Status:string
```

## What `generate` produces

For an `Invoice` entity in an `Invoices` feature, `generate` renders (per adapter) one vertical
slice per command/query under `src/{Solution}.Api/Features/Invoices/...`:

| Operation | Route | Files |
|---|---|---|
| Create | `POST /api/invoices` | `CreateInvoiceCommand.cs`, `CreateInvoiceHandler.cs`, `CreateInvoiceValidator.cs`, `CreateInvoiceEndpoint.cs` |
| Update | `PUT /api/invoices/{id}` | same shape, kind `Update` |
| Delete | `DELETE /api/invoices/{id}` | same shape, kind `Delete`, no validator |
| GetById | `GET /api/invoices/{id}` | `GetInvoiceByIdQuery.cs`, `GetInvoiceByIdHandler.cs`, `GetInvoiceByIdEndpoint.cs` |
| GetAll | `GET /api/invoices` | `GetAllInvoicesQuery.cs` (result wrapped as `IReadOnlyList<T>`), handler, endpoint |

If `--persistence` is anything but `none`, `generate` additionally emits a domain/entity class per
entity (`Persistence/Entities/Invoice.cs` for in-memory and EF Core, `Persistence/Documents/Invoice.cs`
for Marten) and one solution-level file that every entity shares — `InMemoryStore.cs` for in-memory,
a `DbContext` with a `DbSet<T>` per entity for EF Core (Marten needs neither, since `IDocumentSession`
covers every document type generically). Handler bodies get the storage dependency injected —
constructor injection for MediatR, an extra static-method parameter for Wolverine (its own
convention) — and contain real Save/Find/Remove (in-memory), Add/Find/Remove/SaveChanges (EF Core),
or Store/Load/Delete/Query (Marten) calls instead of a placeholder `throw`.

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
  ArchitectLuna.Core.Tests           Fast xUnit unit tests: naming, routing, CRUD synthesis,
                                      protected regions, model validation, YAML round-tripping
  ArchitectLuna.EndToEnd.Tests       Slow xUnit tests that shell out to the real built CLI and run
                                      `dotnet build` on generated output — every adapter x
                                      persistence combination, plus protected-region regeneration
.github/workflows/ci.yml             Build+test on every push/PR, plus a scaffold/generate/build
                                      smoke matrix across every adapter x persistence combination
```

Targets **.NET 10**. Build with `dotnet build ArchitectLuna.sln`, test the fast suite with
`dotnet test tests/ArchitectLuna.Core.Tests`, or everything (including the slow end-to-end suite)
with `dotnet test ArchitectLuna.sln`.

## Status

M1–M4 are done: every adapter × persistence combination generates a compiling solution end to end,
verified by actually scaffolding and building sample projects (automated in
`ArchitectLuna.EndToEnd.Tests` for every adapter × {none, in-memory, efcore-postgres,
efcore-sqlserver, marten} combination, not just eyeballed by hand). Entity-driven CRUD synthesis,
a zero-setup in-memory persistence provider (the `new api` default), real EF Core/Marten
persistence, a CI pipeline, and a model-editing UI are all implemented — a freshly scaffolded
solution has working Create/Read/Update/Delete endpoints with no external dependency required. See
`docs/ROADMAP.md` for what's next (`adapter switch`, EF Core migrations, Clean Architecture
layering as an alternative to vertical slice, additional adapters).
