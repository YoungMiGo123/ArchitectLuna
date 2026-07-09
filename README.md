# ArchitectLuna

ArchitectLuna is a CLI code generator for .NET APIs — think "Intent Architect Lite." A small
YAML **Intent Model** (`.architect/model.yaml`) describes a solution's features and entities;
`architect-luna generate` turns that into a working vertical-slice API: commands, handlers,
validators, and minimal-API endpoints — with real persistence wired in if you ask for it.

It supports two interchangeable backend adapters — **MediatR** and **Wolverine** — that produce
different implementations from the *same* model, and three interchangeable persistence providers —
**EF Core / Postgres**, **EF Core / SQL Server**, and **Marten** — that plug real CRUD into
generated handlers. Switching `--adapter` changes how a request is dispatched (MediatR's `ISender`
vs Wolverine's `IMessageBus`); switching `--persistence` changes how a handler talks to storage.
Neither ever changes the route shape or HTTP surface, because every adapter renders endpoints from
the same shared templates.

## The core idea: entity outwards

An **entity** is the source of truth for a feature's domain data. Everything downstream —
commands, queries, handlers, validators, endpoints, and (if persistence is configured) the actual
storage class — is generated *outward* from the entity, not hand-authored independently of it:

```
entity ──▶ commands (Create/Update/Delete) ──▶ handlers (real CRUD, if --persistence set) ──▶ validators ──▶ endpoints
       └─▶ queries (GetById/GetAll)        ──▶ handlers (real reads,  if --persistence set)                └─▶ endpoints
```

One `add entity` call gives you a full Create/Read/Update/Delete/List slice. You can still hand-add
bespoke commands/queries beyond CRUD with `add command` / `add query` when you need something an
entity's standard shape doesn't cover.

## Quick start

```bash
# Scaffold a new API solution (creates the .sln, an ASP.NET Core minimal-API project,
# and .architect/model.yaml)
architect-luna new api BillingService --adapter mediatr --persistence efcore-postgres
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

# Render the model to real C# files — with efcore-postgres configured, handlers get a real
# DbContext + entity class + working Add/Find/Remove/SaveChanges CRUD, not a placeholder throw
architect-luna generate

dotnet build
```

`--adapter` is `mediatr` or `wolverine` (default `mediatr`). `--persistence` is `none` (default),
`efcore-postgres`, `efcore-sqlserver`, or `marten`.

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

If `--persistence` is anything but `none`, `generate` additionally emits a domain class per entity
(`Persistence/Entities/Invoice.cs` for EF Core, `Persistence/Documents/Invoice.cs` for Marten) and,
for EF Core, one solution-level `DbContext` with a `DbSet<T>` per entity. Handler bodies get the
storage dependency injected — constructor injection for MediatR, an extra static-method parameter
for Wolverine (its own convention) — and contain real Add/Find/Remove/SaveChanges (EF Core) or
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

## Contributing / working with AI agents

Every non-trivial change follows the structured workflow in `docs/workflow/feature-workflow.md`:
classify complexity (S/M/L), auto-plan complex work as a document in `docs/plans/`, implement
against the plan, and verify across the adapter × persistence matrix. `AGENTS.md` is the entry
point for any AI coding agent; in Claude Code, the `/feature` skill drives the whole workflow.
Recurring change types (new adapter, new persistence provider, CLI command, template change) have
step-by-step checklists in `docs/workflow/checklists/`.

## Status

M1–M4 are done: every adapter × persistence combination generates a compiling solution end to end,
verified by actually scaffolding and building sample projects (now automated in
`ArchitectLuna.EndToEnd.Tests`, not just eyeballed by hand). Entity-driven CRUD synthesis, real EF
Core/Marten persistence, a CI pipeline, and a model-editing UI are all implemented. See
`docs/ROADMAP.md` for what's next (`adapter switch`, Clean Architecture layering as an alternative
to vertical slice, additional adapters).
