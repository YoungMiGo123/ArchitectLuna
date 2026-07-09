# Plan 003: Generation quality, entity sync, and database readiness

- **Status:** In progress
- **Complexity:** L (spans `Core/Generation`, `Core/Model`, both adapters, all three persistence
  providers, the CLI, and the YAML schema)
- **Author:** Claude (agent)
- **Date started:** 2026-07-09
- **Checklist used:** none — spans multiple recurring change types (new CLI commands, template
  change, persistence-provider change); each relevant checklist's steps are folded into the step
  list below instead of run standalone.

## Summary

Requirements doc: `docs/requirements/003-improvements.md`.

Today a generated solution builds and serves real CRUD, but needs manual cleanup before it feels
production-ready: there's no way to add a field to an existing entity without hand-editing every
dependent file, validators are 100% explicit-`--rule` (no sensible type-based defaults), generated
code is never auto-formatted, EF Core solutions can't run `dotnet ef migrations add` out of the box
(no `Design` package, no design-time factory), there's no configurable database-apply-mode, Docker
Compose has no health checks, and the Contracts project/target adds a layer of indirection the
team no longer wants (DTOs should live inside the owning Application feature slice instead). This
plan closes all of that: entity field add + full dependent-artifact sync, Contracts folded into
per-feature `Contracts/` folders (Contracts project removed), auto-formatting, field-type-based
validation defaults layered under explicit rules, EF Core migration tooling, configurable
database-apply modes for EF Core and Marten, Docker health checks, richer per-solution README, and
compound commands that offer to create a missing feature/entity instead of just failing.

Two items from the requirements doc are **out of scope** by design decision (see "Out of scope"):
grouped-vs-split file layout (§3.3) is deferred, and "per-service README" (§13) is delivered as
the existing single solution-level README with expanded content, since the tool only ever
scaffolds one API per model — there is no second "service" to document separately.

## Affected components

| Component | Change |
|---|---|
| `ArchitectLuna.Core` | `Model/`: new `DatabaseSettings`/`ApplyMode` on `ArchitectModel`; `ModelEditor`: `AddField`/field-sync entry point; `Naming`/new `DefaultValidationRules` (field-type → default FluentValidation rule inference); `ModelValidator`: validate new schema fields. |
| `ArchitectLuna.Templates` (`.sbn` files) | `Validator.cs.sbn` unchanged (still renders `field.rules`); rule *sourcing* changes upstream, not the template. |
| `ArchitectLuna.Adapters.MediatR` / `.Wolverine` | Emit Request/Response DTOs into the Application feature slice's `Contracts/` folder instead of `GenerationContext.Contracts`; both adapters mirrored. |
| `ArchitectLuna.Persistence.EfCore` | Design-time `DbContext` factory generation; `Microsoft.EntityFrameworkCore.Design` package wiring; apply-mode-aware startup migration call. |
| `ArchitectLuna.Persistence.Marten` | Apply-mode-aware `StoreOptions` schema handling. |
| `ArchitectLuna.Cli` (commands, registries, scaffolder) | New `add field` (alias `update entity --add-field`) and `sync entity` commands; `--yes`/`--create-missing` on `add entity`/`add crud`; `config set database.applyMode`; `GenerationContext` loses the `Contracts` target; `SolutionScaffolder` stops creating the Contracts project; `dotnet format` after `generate`/`new api` (`--no-format`); EF Design package + factory wiring; Docker health checks; README content. |
| `ArchitectLuna.Ui` | Unaffected — reads/writes `ArchitectModel` through `Core` only; new schema fields default sensibly so existing UI flows keep working. |
| Tests (`Core.Tests` / `Template.Tests` / `EndToEnd.Tests`) | New coverage per step below. |
| Docs / CI | `docs/ARCHITECTURE.md` (Contracts section rewritten, apply-mode section added), `README.md`, `docs/ROADMAP.md` moved-to-Done entry. CI matrix unaffected (no new adapter/provider axis). |

## Design decisions

1. **Contracts folds into the Application feature slice, for both layouts.** Today vertical slice
   already collapses `Contracts` into the Api project; Clean Architecture has a real fifth
   project. The requirement wants DTOs in `Application/Features/{Feature}/{Op}/Contracts/` in
   *both* layouts, with the Api project holding only HTTP concerns. Rejected alternative: keep the
   Clean Architecture Contracts project and just add a Contracts *folder* — rejected because the
   requirement is explicit ("The separate Contracts project must be removed") and a project some
   users don't want is worse than a folder everyone gets.
2. **Field-type defaults layer *under* explicit rules, never replace them.** `DefaultValidationRules`
   returns a rule list inferred from `FieldModel.Type`/`Name`; the validator's final rule set is
   `defaults ++ explicitRules` with a defaults suppressed for any field where an explicit rule
   already exists on the same intent (e.g. explicit `NotEmpty()`-equivalent) — simplified to: emit
   the default rule(s) first, then all explicit rules, no de-duplication logic beyond "explicit
   rules always win the last word" since FluentValidation allows multiple `RuleFor` chains safely.
   Rejected alternative: making defaults opt-out per field — rejected, adds a flag nobody asked
   for; `--rule` already lets a user override/extend.
3. **`generate` always syncs; `sync entity`/`add field` are thin wrappers, not new pipelines.**
   Every dependent artifact (validators, mappings, handlers, persistence config) is already
   *re-rendered from the model* on every `generate` run — the gap is only that there's no CLI verb
   to add a field to an *existing* entity's model. So `add field` mutates `EntityModel.Fields` via
   `ModelEditor`, then the command internally calls the same generation pipeline `generate` uses
   (protected regions already survive this). `sync entity` becomes a documented alias that just
   re-runs generation for entities already in the model with no field change — useful after manual
   `model.yaml` edits. Rejected alternative: a bespoke diff/patch engine — rejected as unnecessary
   complexity; regenerate-from-model-with-protected-regions already *is* the sync mechanism.
4. **Database apply mode is a new top-level `database:` block on `ArchitectModel`**, not a separate
   `.architect/architect.json`. Rejected the separate-file design in the requirement doc's §9.2
   ("or equivalent") because `model.yaml` is already the single source of truth per
   `docs/ARCHITECTURE.md` and a second config file would fork that. `config set database.applyMode
   <mode>` becomes a small `ModelEditor` mutation + save, not a new file format.
5. **`on-startup` apply for EF Core calls `context.Database.Migrate()`** in the generated
   `AddInfrastructure`/startup path (gated by apply mode), which requires at least one migration to
   exist — documented in the README as a caveat (no migration ⇒ no-op). **`on-generate`** shells out
   to `dotnet ef database update` from `GenerateCommand` after rendering, best-effort (a missing
   local Postgres/SQL Server must not fail `generate` — warn and continue). **Marten** has no
   migrations concept; `manual`/`on-generate` leave `StoreOptions.AutoCreateSchemaObjects` at
   `CreateOnly`/default, `on-startup` sets `AutoCreateSchemaObjects = All` and calls
   `ApplyAllConfiguredChangesToDatabaseAsync` at startup.
6. **EF Design package goes on Infrastructure** (where the DbContext lives), matching the
   requirement's explicit example (`--project src/FeedbackService.Infrastructure`). For vertical
   slice (one physical project), it's the same csproj `AddInfrastructure` packages already target.
7. **Compound-command confirmation uses `System.Console` prompts already available to
   `Spectre.Console.Cli`** (the CLI already depends on `Spectre.Console`) — `IAnsiConsole.Confirm`.
   Non-interactive detection: `--yes`/`--create-missing` flags are equivalent aliases (per spec
   §8.1, either name is accepted); without either flag and without a TTY, fail with a clear error
   rather than hanging on a prompt that can't be answered.

## Invariant check

- [x] Adapter parity preserved — every adapter-facing change (Contracts-folder emission) is made
  identically in `MediatRAdapter` and `WolverineAdapter`; verified by keeping both diffs
  symmetric and running the full E2E matrix.
- [x] Core stays framework-free — `DefaultValidationRules`, `DatabaseSettings`, `ModelEditor.AddField`
  live in `ArchitectLuna.Core` with no FluentValidation/EF/Marten/Scriban references; EF/Marten-
  specific apply-mode behavior lives in their own provider projects.
- [x] Adapters do no file I/O — Contracts-folder DTOs are still returned as `GeneratedFile` records
  by the adapters; `FileWriter` still owns writing.
- [x] Protected regions survive regeneration — `add field`/`sync entity` route through the existing
  generate pipeline, which already merges protected regions; verified by extending
  `ProtectedRegionRegenerationTests` with an add-field round trip.
- [x] `HandlerBinding` single-dependency cap respected — untouched; no new persistence dependency
  shape introduced.
- [x] Template gotchas — no new `.sbn` embedded resources added in this plan (Contracts-folder
  change is a path change on existing `Request.cs.sbn`/`Response.cs.sbn`-equivalent inline
  rendering already in the adapters, not new template files); confirmed no `Handler.cs.sbn`
  async/CancellationToken regression from apply-mode changes (those live in
  `AddInfrastructure`/`Program.cs`, not handlers).

## Steps

1. **Model schema: `database.applyMode` + `add field` model support** — files:
   `ArchitectLuna.Core/Model/ArchitectModel.cs` (new `DatabaseSettings` w/ `ApplyMode` enum
   `Manual|OnGenerate|OnStartup`, default `Manual`), YAML serializer round-trip, `ModelValidator`,
   `ModelEditor.AddFieldToEntity(...)` — verify: `dotnet test tests/ArchitectLuna.Core.Tests`
   (new `ModelEditorAddFieldTests`, `ArchitectModelYamlRoundTripTests` extension), plus a
   YAML fixture showing `database: { applyMode: on-startup }` parses and re-serializes losslessly.
2. **CLI: `add field` / `update entity --add-field`, `sync entity`, `config set
   database.applyMode`** — files: new `Commands/AddFieldCommand.cs`, `Commands/SyncEntityCommand.cs`,
   `Commands/ConfigSetCommand.cs`, registered in `Program.cs`'s command app — verify: new
   `AddFieldCommandTests`/`SyncEntityCommandTests`/`ConfigSetCommandTests` in
   `ArchitectLuna.Cli.Tests` (or `EndToEnd.Tests` if that's where CLI command tests live —
   confirmed location during implementation), each asserting the model file changes and (for
   `add field`) that a subsequent `generate` updates the entity class, validator, mappings,
   handler signatures, and persistence config for that entity across all three providers.
3. **Field-type-based default validation** — files: new `ArchitectLuna.Core/Naming/
   DefaultValidationRules.cs` (pure function `FieldModel -> IReadOnlyList<string>`), wired into
   both adapters' rule-building right before they call the existing `Validator.cs.sbn` render (so
   the template itself is untouched) — verify: `Core.Tests` unit tests per type/name heuristic in
   the requirement (`string→NotEmpty`, `Guid→NotEmpty`, `pageSize/pageNumber→GreaterThan(0)`,
   `*email*→EmailAddress`, `*currency*→MaximumLength(3)`, `bool→none`), plus a `Template.Tests`
   snapshot proving explicit `--rule` entries still render and defaults don't collide/duplicate.
4. **Auto-formatting** — files: `Cli/Scaffolding/SolutionScaffolder.cs` and
   `Commands/GenerateCommand.cs` shell out to `dotnet format <sln>` after writing files; `--no-format`
   flag on `generate`/`new api` — verify: an `EndToEnd.Tests` case asserting a deliberately
   misindented generated file is reformatted after `generate`, and that `--no-format` skips it
   (compare file bytes before/after).
5. **Result/exception-middleware polish** — files: `Cli/Scaffolding/FoundationFiles.cs`
   (`BuildExceptionHandlingMiddleware`) adds `DbUpdateConcurrencyException` → 409 Conflict and
   `DbUpdateException` → 500 (logged) catch arms. Implemented as `catch (Exception ex) when
   (ex.GetType().Name == "DbUpdateConcurrencyException")` — matched by type name rather than a
   `catch (DbUpdateException ex)` clause, so the one generated middleware file works unchanged for
   every persistence provider without the Api project needing an EF Core package reference it
   otherwise has no reason to carry (simpler than the originally-planned per-provider gating, same
   behavior) — verify: `Template.Tests` snapshot confirming both catch arms are present and their
   status codes are correct.
6. **EF Core Design package + design-time factory** — files:
   `ArchitectLuna.Persistence.EfCore/EfCorePersistenceGenerator.cs` adds
   `Microsoft.EntityFrameworkCore.Design` to Infrastructure's (or the single vertical-slice
   project's) `RequiredPackages`; new `GenerateSolutionPersistence` output
   `Persistence/{Solution}DbContextFactory.cs` implementing
   `IDesignTimeDbContextFactory<{Solution}DbContext>`, reading the local connection string the
   same way `appsettings.Development.json` does — verify: `EndToEnd.Tests` runs the exact
   `dotnet ef migrations add Initial --project ... --startup-project ...` command from the
   requirement doc against a scaffolded `efcore-postgres` and `efcore-sqlserver` solution and
   asserts exit code 0 and a `Migrations/` folder appears.
7. **Database apply-mode wiring (EF Core + Marten)** — files:
   `EfCorePersistenceGenerator.BuildServiceRegistration` gains an apply-mode-conditional
   `Database.Migrate()` call in `AddInfrastructure` for `on-startup`;
   `Commands/GenerateCommand.cs` shells out `dotnet ef database update` best-effort for
   `on-generate` (catch and warn, don't fail the command); `MartenPersistenceGenerator` sets
   `StoreOptions.AutoCreateSchemaObjects`/applies changes per mode — verify: `Template.Tests`
   snapshots per apply mode × EF Core, and a Marten `EndToEnd.Tests` case running the generated
   API against a local Postgres with `on-startup` and confirming tables exist after boot with zero
   manual migration step.
8. **Docker Compose health checks** — files: `Cli/Scaffolding/InfrastructureFiles.cs` — `Dockerfile`
   gets a `HEALTHCHECK` hitting `/health`; `docker-compose.yml`'s `db` service gets a
   provider-appropriate healthcheck (`pg_isready` / `sqlcmd`), `api` service gets
   `depends_on: db: condition: service_healthy` when a DB service exists — verify: `Template.Tests`
   snapshot of compose/Dockerfile content; manual `docker compose config` validation (no live
   Docker needed) during implementation.
9. **Contracts removal — GenerationContext + scaffolder** — files:
   `Core/Generation/GenerationContext.cs` drops the `Contracts` target entirely (both
   `ForVerticalSlice`/`ForCleanArchitecture` stop taking/returning it);
   `Cli/Scaffolding/SolutionScaffolder.cs` stops creating `src/{Solution}.Contracts` (no csproj, no
   sln entry, no ProjectReference from Application/Api); `InfrastructureFiles.ArchitectureDoc`
   rewritten to describe the four-project (Clean Architecture) / one-project (vertical slice)
   shape with Contracts folders — verify: `Template.Tests` snapshot of the generated `.sln`/project
   list per layout; existing Contracts-target tests updated or removed.
10. **Contracts removal — adapters emit into feature-slice `Contracts/` folder** — files:
    `Adapters.MediatR/MediatRAdapter.cs` and `Adapters.Wolverine/WolverineAdapter.cs` (`Slice`
    helper, `GenerateCommand`/`GenerateQuery`) compute `ContractsPath`/`ContractsNamespace` under
    `{Application.ProjectRoot}/Features/{Feature}/{Op}/Contracts` /
    `{Application.RootNamespace}.Features.{Feature}.{Op}.Contracts` instead of
    `context.Contracts.*`; render models (`CommandEndpointRenderModel`, `QueryEndpointRenderModel`,
    `MappingsRenderModel`, `RecordRenderModel`) updated for the new namespace shape — verify:
    `Template.Tests` snapshot of a generated command/query's file set and namespaces for both
    adapters; full `EndToEnd.Tests` matrix (adapter × persistence × layout) since this changes
    every generated file's using/namespace.
11. **Compound commands — automatic missing-feature/entity creation** — files:
    `Commands/AddEntityCommand.cs`/`AddCrudCommand.cs` check `ModelEditor` for the feature's (and,
    for `add crud`, the entity's) existence; on missing feature, prompt via `IAnsiConsole.Confirm`
    (or honor `--yes`/`--create-missing`) before creating it; `add crud` with a missing entity
    prints the "create it first with `add entity ...`" guidance and exits non-zero instead of
    creating a meaningless entity — verify: CLI command tests covering: interactive-confirm-yes,
    interactive-confirm-no (cancelled, non-zero exit, no model mutation), `--yes` non-interactive
    success, non-interactive-no-flag failure with clear message, `add crud` on missing entity.
12. **README content expansion** — files: `Cli/Scaffolding/InfrastructureFiles.ReadMe(...)` gains
    sections for: selected database apply mode, "How to add a field"
    (`architect-luna add field <feature> <entity> <name>:<type>`), the exact `dotnet ef
    migrations add`/`database update` commands from the requirement doc (EF Core solutions only),
    Marten schema-handling note (Marten solutions only), `docker compose up --build` — verify:
    `Template.Tests` snapshot asserting each section is present/absent per persistence provider.

## Test plan

- **Unit (`ArchitectLuna.Core.Tests`):** `DefaultValidationRules` per-type/name-heuristic table
  test; `ModelEditor.AddFieldToEntity` (happy path, unknown entity, duplicate field name);
  `DatabaseSettings` YAML round-trip; `ModelValidator` accepts/rejects apply-mode values.
- **Snapshot (`ArchitectLuna.Template.Tests`):** exception-middleware catch-arm set per
  persistence provider; Docker/compose content; README section presence; generated file set/
  namespaces for a command and a query under the new Contracts-folder shape, both adapters, both
  layouts; validator output combining default + explicit rules.
- **E2E (`ArchitectLuna.EndToEnd.Tests`):** full adapter × persistence × layout matrix must still
  scaffold + generate + `dotnet build` clean after the Contracts-folder change (step 10) and the
  apply-mode wiring (step 7) — this is the step most likely to regress compilation across the
  matrix, so it gets the widest re-run. New cases: `dotnet ef migrations add`/`database update`
  against `efcore-postgres`/`efcore-sqlserver` scaffolds (step 6); `add field` end-to-end updating
  every dependent file (step 2); `dotnet format` idempotency (step 4); compound-command prompts
  (step 11, via piped stdin for the interactive cases).
- **Manual verification:** none beyond what E2E already automates — no live Docker/Postgres/SQL
  Server dependency is assumed available in this sandbox, so `on-startup`/`docker compose up`
  behavior is verified by generated-content assertions (step 7/8) rather than actually booting
  containers, unless a local Postgres is confirmed reachable during implementation.

## Out of scope

- **§3.3 grouped/split operation-layout flag.** The default (and, per the requirement, already-
  required) shape — command + result + handler in one file named after the handler — is what the
  adapters already produce; this plan does not add the `split` alternative. Flagged in
  `docs/ROADMAP.md` as a follow-up.
- **Per-service README (§13) as a literal second README.** ArchitectLuna scaffolds one API per
  model; "the service" and "the solution" are the same thing today, so this plan expands the
  existing solution README rather than inventing a second document.
- **Non-Postgres/SQL Server Marten backends, and any persistence provider beyond the existing
  three.** Apply-mode work targets EF Core (both providers) and Marten only, matching the
  requirement doc.
- **Live-database verification of `on-startup`/`on-generate` apply modes** unless a reachable
  Postgres/SQL Server is confirmed available in this environment — covered by generated-content
  assertions otherwise (see Test plan).
- **UI (`ArchitectLuna.Ui`) support for any of the new CLI verbs** (`add field`, `config set`,
  compound-command prompts). The UI's add-entity form is unaffected; a UI follow-up is a separate,
  smaller change left to `docs/ROADMAP.md`.

## Outcome (fill in at delivery)

- What shipped, with commit hashes.
- Deviations from the plan above and why.
- Follow-ups discovered (add real ones to `docs/ROADMAP.md`).
