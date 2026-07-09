# Agent Guide — ArchitectLuna

Instructions for any AI coding agent (Claude, Copilot, Cursor, Codex, …) working in this repo.

## What this project is

A CLI code generator for .NET APIs: a YAML Intent Model (`.architect/model.yaml`) is rendered
into vertical-slice APIs through two seams — `IFrameworkAdapter` (MediatR/Wolverine) and
`IPersistenceGenerator` (EF Core Postgres/SQL Server, Marten). Read `README.md` and
`docs/ARCHITECTURE.md` before changing anything; `docs/ROADMAP.md` lists planned work.

## The workflow — follow it for every change

**`docs/workflow/feature-workflow.md`** defines the required process:

1. **Intake** — restate the goal; read the bounding docs.
2. **Classify** — S/M/L against the rubric. Automatic **L** (full plan doc required): new
   adapter/provider/CLI verb, any change under `ArchitectLuna.Core/Generation/`, model-schema
   changes, anything spanning the adapter × persistence matrix.
3. **Plan** — L: copy `docs/workflow/templates/feature-plan.md` to `docs/plans/NNN-slug.md` and
   complete it *before writing code*. M: same sections inline. Recurring change types have
   ready-made checklists in `docs/workflow/checklists/` (new adapter, new persistence provider,
   CLI command, template change) — use them instead of improvising.
4. **Implement** — in plan-step order; keep the plan doc updated when reality diverges.
5. **Verify & deliver** — see commands below; update docs made stale; fill the plan's Outcome
   section; commit to a feature branch, never `main`.

## Build & test

```bash
dotnet build ArchitectLuna.sln                    # always
dotnet test tests/ArchitectLuna.Core.Tests        # fast suite — Core/naming/model/merge changes
dotnet test tests/ArchitectLuna.Template.Tests    # fast in-memory snapshot suite — any change to
                                                  # generated-output shape (templates, adapters,
                                                  # foundation files, Program.cs)
dotnet test ArchitectLuna.sln                     # full incl. slow E2E (scaffolds + builds real
                                                  # solutions across adapter × persistence matrix)
                                                  # — required for template/adapter/provider/CLI changes
```

Targets .NET 10. E2E tests shell out to the real `dotnet` CLI.

## Non-negotiable invariants

(Details and rationale: `docs/workflow/feature-workflow.md` §Stage 4, `docs/ARCHITECTURE.md`.)

1. Both adapters produce the identical HTTP surface — shared endpoint templates, shared
   `RouteInference`. Behavior added to one adapter is added to the other.
2. `ArchitectLuna.Core` never references MediatR/Wolverine/EF Core/Marten/Scriban. Only the CLI
   registries (`AdapterRegistry`, `PersistenceRegistry`) know concrete names.
3. Adapters return `GeneratedFile` records; only `FileWriter` writes files.
4. Protected regions (`// <architect:region ...>`) must survive regeneration.
5. `HandlerBinding` injects at most one dependency.
6. Template gotchas: new `.sbn` embedded resources need `WithCulture="false"`; handlers are
   unconditionally `async`; Wolverine handlers declare `CancellationToken` explicitly.

## Conventions

- Code style: match surrounding code; see `docs/PATTERNS.md`.
- Commits: describe the behavior change; reference `docs/plans/NNN-*.md` when one exists; small
  coherent commits.
- Completed roadmap items move to **Done** in `docs/ROADMAP.md` as part of the change, not later.
