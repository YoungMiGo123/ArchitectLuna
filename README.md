# ArchitectLuna

ArchitectLuna is a CLI code generator for .NET APIs — think "Intent Architect Lite." A small
YAML **Intent Model** (`.architect/model.yaml`) describes a solution's features and entities;
`architect-luna generate` turns that into a working vertical-slice API: commands, handlers,
validators, and minimal-API endpoints.

It supports two interchangeable backend adapters — **MediatR** and **Wolverine** — that produce
different implementations from the *same* model. Switching `--adapter` changes how a request is
dispatched (MediatR's `ISender` vs Wolverine's `IMessageBus`); it never changes the route shape
or the HTTP surface, because both adapters render endpoints from the same shared templates.

## The core idea: entity outwards

An **entity** is the source of truth for a feature's domain data. Everything downstream —
commands, queries, handlers, validators, endpoints — is generated *outward* from the entity, not
hand-authored independently of it:

```
entity ──▶ commands (Create/Update/Delete) ──▶ handlers ──▶ validators ──▶ endpoints
       └─▶ queries (GetById/GetAll)        ──▶ handlers                └─▶ endpoints
```

One `add entity` call gives you a full Create/Read/Update/Delete/List slice. You can still hand-add
bespoke commands/queries beyond CRUD with `add command` / `add query` when you need something an
entity's standard shape doesn't cover.

## Quick start

```bash
# Scaffold a new API solution (creates the .sln, an ASP.NET Core minimal-API project,
# and .architect/model.yaml)
architect-luna new api BillingService --adapter mediatr   # or --adapter wolverine
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

# Render the model to real C# files
architect-luna generate

dotnet build
```

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

Every endpoint implements a shared `IEndpointDefinition` interface; `Program.cs` discovers and
maps them by reflection, so it never needs to be regenerated as features grow.

Handler bodies are wrapped in `// <architect:region name="handler-body"> ... // </architect:region>`
markers. Re-running `generate` after you've filled in real logic preserves everything inside the
markers and regenerates everything else — so evolving the model (new fields, a renamed command)
never silently discards your hand-written business logic.

## Repo layout

```
src/
  ArchitectLuna.Cli                  Spectre.Console.Cli commands: new, add, generate
  ArchitectLuna.Core                 Intent Model (IR), naming, route inference, validation,
                                      manifest, protected-region merging — no framework knowledge
  ArchitectLuna.Templates            Scriban .sbn templates (embedded resources) + engine
  ArchitectLuna.Adapters.MediatR     IFrameworkAdapter implementation for MediatR
  ArchitectLuna.Adapters.Wolverine   IFrameworkAdapter implementation for Wolverine
tests/
  ArchitectLuna.Core.Tests           xUnit tests for naming, routing, CRUD synthesis, protected
                                      regions, model validation, YAML round-tripping
```

Targets **.NET 10**. Build with `dotnet build ArchitectLuna.sln`, test with
`dotnet test tests/ArchitectLuna.Core.Tests`.

## Status

M1–M4 are done: both adapters generate a compiling solution end to end, verified by actually
scaffolding and building sample projects (not just unit tests). Entity-driven CRUD synthesis is
implemented on top of that. See `docs/ROADMAP.md` for what's next (EF Core-backed persistence,
`adapter switch`, a UI layer, additional adapters).
