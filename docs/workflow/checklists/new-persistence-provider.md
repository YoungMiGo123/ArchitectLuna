# Checklist: Add a new persistence provider

Complexity: **L** — full plan document required (`docs/workflow/feature-workflow.md` §Stage 3).
Reference implementations: `ArchitectLuna.Persistence.EfCore` (relational, has solution-level
DbContext), `ArchitectLuna.Persistence.Marten` (document, nothing solution-level).

A provider owns *how a handler talks to storage*. It never changes routes, message shapes, or the
HTTP surface, and it must work identically under every adapter.

## Steps

- [ ] **New project** `src/ArchitectLuna.Persistence.<Name>/`, referencing only
      `ArchitectLuna.Core` (and `ArchitectLuna.Templates` if it renders from `.sbn`). Add to the
      solution.
- [ ] **Generator class** `<Name>PersistenceGenerator : IPersistenceGenerator` implementing:
      - `GetHandlerBinding` — body statements per command/query kind (Create/Update/Delete/
        GetById/GetAll) plus **at most one** injected dependency (hard cap — see the doc comment
        in `Core/Generation/HandlerBinding.cs`). Bodies may `await` and must pass
        `cancellationToken` to async storage calls.
      - `GenerateEntityPersistence` — per-entity files (domain/document class, type
        configuration, …).
      - `GenerateSolutionPersistence` — solution-level files given all entities (a DbContext
        equivalent); return an empty list if the store needs none (like Marten).
- [ ] **Registry**: add the CLI name to `PersistenceRegistry.KnownProviders`, a case to both
      `Resolve` overloads and to `ParseProvider`, and a member to the `PersistenceProvider` enum
      in `ArchitectLuna.Core/Model/` (`src/ArchitectLuna.Cli/Adapters/PersistenceRegistry.cs`).
- [ ] **Scaffolding**: teach `SolutionScaffolder` the NuGet package(s) and the `Program.cs`
      service registration (connection string plumbing) a scaffolded API needs.
- [ ] **Model round-trip**: confirm the new `PersistenceProvider` enum value serializes/parses
      through `model.yaml` (`ModelSerializerTests`).
- [ ] **Tests**:
      - `GeneratedSolutionBuildTests.cs`: add `InlineData` rows for **both** adapters × `<name>`.
      - Unit-test the binding shape in `Core.Tests` if the provider has non-obvious body logic.
- [ ] **CI**: add the provider to the smoke-matrix axis in `.github/workflows/ci.yml`.
- [ ] **Docs**: README (`--persistence` values, what `generate` emits for this provider),
      `docs/ARCHITECTURE.md` component map, `docs/ROADMAP.md`.

## Definition of done

`dotnet test ArchitectLuna.sln` green including new rows: a scaffolded solution using the new
provider **builds** under both adapters, handlers contain real storage calls (not the
`NotImplementedException` placeholder), and protected-region regeneration still preserves
hand-written handler bodies.
