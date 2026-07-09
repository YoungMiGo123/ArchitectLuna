# Plan 001: Production foundation + testing layer

- **Status:** In progress
- **Complexity:** L
- **Author:** Claude (claude-fable-5 session)
- **Date started:** 2026-07-09
- **Checklist used:** none directly (the change spans several types); `new-cli-command.md` steps
  folded into step 6 for `add crud`, `template-change.md` discipline applied throughout step 5.

## Summary

Implements the two requirement documents now checked in at
`docs/requirements/001-implementation-architecture.md` and
`docs/requirements/002-testing-layer.md`. After this change, every scaffolded solution ships the
full production foundation (Result pattern, `BaseEntity`, user-context and date/time abstractions,
correlation-ID + exception middleware, Serilog request logging, mapping layer with
Request/Response DTOs, a Contracts project under Clean Architecture) behind clean extension
methods (`AddApi`/`AddApplication`/`AddInfrastructure`/`UseApiMiddleware`/`MapApiEndpoints`/
`UseApiLogging`) so `Program.cs` stays small; handlers return `Result<T>` and endpoints map
results to consistent HTTP status codes; generation ordering gains an `add crud` verb with clear
error paths; and the test suite gains the three-layer shape the testing doc requires — categorized
Core unit tests, a new in-memory `ArchitectLuna.Template.Tests` snapshot project, and expanded
E2E ordering/matrix/generated-project-test coverage — wired into CI.

## Affected components

| Component | Change |
|---|---|
| `ArchitectLuna.Core` | `GenerationContext` gains a fifth `ProjectTarget` (`Contracts`) + `HasSeparateContracts`; `IPersistenceGenerator` registration hooks reshaped (`BuildServiceRegistration`/`ServiceRegistrationUsings` emitting `services.`/`configuration.` lines for the generated `AddInfrastructure` extension); new `Editing/ModelEditor` owning add-feature/entity/command/query/crud mutations + ordering/duplicate rules (extracted from CLI so they're unit-testable); `NullPersistenceGenerator` updated. |
| `ArchitectLuna.Templates` (`.sbn` files) | `Message`/`Handler` (both adapters) return `Result<T>`; `Shared/CommandEndpoint`/`QueryEndpoint` rewritten for Request DTO binding, mapping calls, and Result→HTTP status mapping; new `Shared/Request.cs.sbn`, `Shared/Response.cs.sbn`, `Shared/Mappings.cs.sbn`. All new embedded resources get `WithCulture="false"`. |
| `ArchitectLuna.Adapters.MediatR` | Slice generation extended: Request/Response/Mappings files, `Result<T>` message/handler/dispatch types, endpoint render models. |
| `ArchitectLuna.Adapters.Wolverine` | Same changes as MediatR (parity), `InvokeAsync<Result<T>>` dispatch. |
| `ArchitectLuna.Persistence.EfCore` / `.Marten` / `.InMemory` | Entity/document classes inherit `BaseEntity`; handler bodies return `Result<T>` (`NotFound` instead of `KeyNotFoundException`); registration hook reshaped. |
| `ArchitectLuna.Cli` (commands, registries, scaffolder) | New `FoundationFiles` builder (pure, testable) for all foundation/extension files; `ProgramCsBuilder` rewritten to the required small shape; `SolutionScaffolder` writes foundation files, scaffolds the Contracts project (Clean Architecture), `.editorconfig`, generated README, `docs/`, `Infrastructure.Tests`; add-* commands delegate to `ModelEditor`; new `add crud` command; `--architecture` default flips to `clean-architecture` per requirement 001 §"Clean Architecture by Default". |
| `ArchitectLuna.Ui` | Unaffected (uses Core model APIs that keep their shape; add-entity page moves to `ModelEditor` only if trivial). |
| Tests (Core.Tests / Template.Tests / EndToEnd.Tests) | Core.Tests reorganized into category folders + new Manifest/GenerationOrdering/Naming cases; new `ArchitectLuna.Template.Tests` project (VerticalSlice + CleanArchitecture: ProductionFoundation/FeatureGenerationSnapshot/CrudGenerationSnapshot tests, in-memory over `GeneratedFile` sets); E2E adds `GenerationOrderingTests`, `GeneratedProjectTestSuiteTests` (runs `dotnet test` inside one generated solution), expands Clean Architecture matrix coverage, updates regression greps for the new output shape. |
| Docs / CI (`.github/workflows/ci.yml`) | CI runs the three test projects explicitly + `dotnet test` on the generated project for one representative smoke combination; README/ARCHITECTURE/ROADMAP updated; requirement docs checked into `docs/requirements/`. |

## Design decisions

1. **Result pattern is generated source, not a package.** `Result`, `Result<T>`, `Error` (+
   `ErrorType`), `ValidationError`, `PagedResult<T>` are scaffold-time files under the Application
   target's `Common/Results`, per the repo's package philosophy ("prefer simple Result types over
   a heavy result library"). Alternative rejected: FluentResults/ErrorOr NuGet dependency.
2. **Handlers return `Result<TResult>`; endpoints translate.** Persistence bindings return
   `Error.NotFound(...)` failures instead of throwing `KeyNotFoundException`; a scaffold-time
   `ResultHttpExtensions.ToProblem()` in Api `Common` maps `ErrorType` → 400/404/409/401/403/500,
   and endpoints map success to 201 (Create), 200 (Update/GetById/GetAll), 204 (Delete).
   `--persistence none` placeholders stay `throw new NotImplementedException();` (still compiles in
   a `Result<T>` handler; keeps requirement 002's "placeholder protected regions" behaviour).
   `ExceptionHandlingMiddleware` stays as the safety net for unhandled/hand-written exceptions.
3. **Fifth `ProjectTarget`: `Contracts`.** Clean Architecture scaffolds a real
   `{Solution}.Contracts` project (referenced by Application and Api; references nothing) holding
   Request/Response DTOs; vertical slice maps `Contracts` to the Api target so DTOs stay inside
   the slice folder. Adapters keep asking "where does the Contracts target live" — no layout
   branching (invariant preserved). DTO paths are uniformly
   `{Contracts.ProjectRoot}/Features/{Feature}/{Operation}/` — slightly deeper than requirement
   001's flat `Contracts/Invoices/` illustration, in exchange for layout-agnostic adapters.
4. **Mapping layer is generated extension methods** (`{Op}Mappings.cs` in the Application slice):
   `ToCommand(this {Op}Request)` (+ route id overload for Update), `ToResponse(this {Op}Result)`.
   Delete gets no Request/Response/Mappings (requirement 001's file table). Alternative rejected:
   Mapster/AutoMapper.
5. **Startup style via generated extension classes.** `FoundationFiles` emits
   `ApiDependencyInjection` (AddApi: Swagger, health checks, `IUserContext`),
   `ApplicationDependencyInjection` (AddApplication: MediatR or validators — adapter-specific
   body), `InfrastructureDependencyInjection` (AddInfrastructure: persistence registration +
   `IDateTimeProvider`), `MiddlewareExtensions` (UseApiMiddleware: correlation ID → exception
   middleware → Serilog request logging → Swagger-in-dev), `EndpointExtensions` (MapApiEndpoints:
   health + `IEndpointDefinition` discovery), `LoggingExtensions` (UseApiLogging). Distinct class
   *and file* names so vertical slice (one project) has no collisions. **Known deviation:**
   Wolverine's `UseWolverine` is `IHostBuilder`-level, so Wolverine solutions keep one extra
   `builder.Host.UseWolverine(...)` line in `Program.cs` — the dispatcher seam is allowed to
   change "framework-specific setup" per requirement 001.
6. **`IPersistenceGenerator` registration hook reshaped**, from Program.cs-splice
   (`BuildProgramCsRegistration`/`ProgramCsUsings`, `builder.`-based lines) to
   `BuildServiceRegistration`/`ServiceRegistrationUsings` (`services.`/`configuration.`-based
   lines) consumed by the generated `AddInfrastructure`. Justified: the seam's job is unchanged
   (per-provider registration), only its call site moved out of `Program.cs`, which requirement
   001 forbids regenerating per feature and requires staying small.
7. **`BaseEntity` in Domain `Common/`** with Id/CreatedAt/CreatedBy/UpdatedAt/UpdatedBy/IsDeleted;
   generated entities and Marten documents inherit it (own `Id` property removed). Automatic
   audit-field population is out of scope (handlers keep at most one injected dependency).
8. **Ordering/duplicate rules move to Core (`ModelEditor`)** so requirement 002's "invalid command
   ordering" and "duplicate detection" categories are fast unit tests, and CLI commands become
   thin presenters of `ModelEditor` errors. `add crud <feature> <entity>` synthesizes any missing
   CRUD operations for an *existing* entity and fails with "create the entity first" guidance
   otherwise.
9. **Snapshot tests are structural assertions in-memory**, not golden files: adapters return
   `GeneratedFile` records and every foundation builder is a pure function, so
   `ArchitectLuna.Template.Tests` asserts file sets + content structure with zero file/process
   I/O. Alternative rejected: Verify/golden-file snapshots (brittle to whitespace, adds a package).
10. **Default architecture flips to `clean-architecture`** per requirement 001 §"Clean
    Architecture by Default". Vertical slice remains fully supported and explicitly exercised
    everywhere in tests/CI.
11. **Exhaustive 16-combination matrix stays in the CI smoke jobs** (parallel, already present,
    plus in-memory = 20 jobs); the xUnit E2E suite covers the full 10-combination vertical-slice
    matrix plus a widened representative Clean Architecture set — running all 20 serially inside
    one test job would blow the CI timeout for no added coverage.
12. **`Directory.Packages.props` (central package management) is out of scope** — scaffolding
    resolves live versions via `dotnet add package`, which CPM would pin differently; revisit if
    the generated-solution package story changes.

## Invariant check

- [x] Adapter parity preserved — endpoints/Request/Response/Mappings render from `Templates/Shared`
      for both adapters; message/handler changes applied to both; Template.Tests asserts both.
- [x] Core stays framework-free — Result pattern etc. are generated *content* built in the CLI
      project; Core gains only model/editing/context types.
- [x] Adapters do no file I/O — new files are additional `GeneratedFile` records.
- [x] Protected regions survive regeneration — handler-body region markers unchanged;
      `ProtectedRegionMergerTests` + `ProtectedRegionRegenerationTests` must stay green.
- [x] `HandlerBinding` single-dependency cap respected — bodies change, dependency count doesn't.
- [x] Template gotchas handled — new `.sbn` resources get `WithCulture="false"`; handlers stay
      unconditionally `async`; Wolverine keeps explicit `CancellationToken`.

## Steps

1. **Check in requirement docs + this plan** — files: `docs/requirements/001-*.md`,
   `docs/requirements/002-*.md`, `docs/plans/001-*.md`, `docs/plans/README.md` — verify: files
   present, solution still builds.
2. **Core seam changes** — files: `Core/Generation/GenerationContext.cs` (Contracts target),
   `Core/Generation/IPersistenceGenerator.cs`, `Core/Generation/NullPersistenceGenerator.cs`,
   new `Core/Editing/ModelEditor.cs` (+ `EditResult`) — verify: `dotnet build` (providers/CLI
   updated in the same step to keep the build green), fast Core suite.
3. **Persistence providers** — files: `Persistence.InMemory/EfCore/Marten` generators —
   `BaseEntity` inheritance, `Result<T>` bodies, reshaped registration hook — verify: build.
4. **Foundation files + scaffolder** — files: new `Cli/Scaffolding/FoundationFiles.cs`,
   `ProgramCsBuilder.cs` (rewritten), `SolutionScaffolder.cs` (foundation files, Contracts
   project, `.editorconfig`/README/docs, Infrastructure.Tests, abstractions packages),
   `InfrastructureFiles.cs`, `TestProjectScaffolder.cs` — verify: build + manual scaffold of one
   solution per layout compiles before first `generate`.
5. **Templates + adapters** — files: all `.sbn` templates, new Request/Response/Mappings
   templates, `Templates/RenderModels/*`, `MediatRAdapter.cs`, `WolverineAdapter.cs`,
   `Templates.csproj` — verify: build + manual scaffold/add/generate/build for
   (vertical-slice, mediatr, in-memory) and (clean-architecture, wolverine, efcore-postgres).
6. **CLI commands** — files: `Commands/Add*.cs` (delegate to ModelEditor), new
   `Commands/AddCrudCommand.cs`, `Program.cs`, `NewApiCommand.cs` (default architecture) —
   verify: build + manual runs of every ordering error path.
7. **Core.Tests reorganization + new categories** — files: `tests/ArchitectLuna.Core.Tests/**`
   (folders: Naming, Routing, ModelValidation, CrudSynthesis, ProtectedRegions, Manifest,
   GenerationOrdering, YamlRoundTripping) — verify: fast suite green.
8. **Template.Tests project** — files: new `tests/ArchitectLuna.Template.Tests/**`, `ArchitectLuna.sln`
   — verify: `dotnet test tests/ArchitectLuna.Template.Tests` green.
9. **E2E suite updates** — files: `tests/ArchitectLuna.EndToEnd.Tests/**` (ordering tests,
   generated-project `dotnet test` case, widened clean-arch matrix, updated regression greps) —
   verify: targeted E2E runs green.
10. **CI** — files: `.github/workflows/ci.yml` — verify: YAML lints, step commands match the
    three-project layout + representative generated-project `dotnet test`.
11. **Docs** — files: `README.md`, `docs/ARCHITECTURE.md`, `docs/ROADMAP.md`, this plan's Outcome
    — verify: statements match shipped behaviour.
12. **Full verification** — `dotnet build ArchitectLuna.sln` + `dotnet test ArchitectLuna.sln`
    (includes full E2E matrix) — verify: everything green, then push.

## Test plan

- **Core.Tests:** ModelEditor ordering/duplicate rules (feature-before-entity, entity-before-crud,
  bespoke command/query allowed without entity, duplicate feature/entity/command/query, crud name
  collisions), manifest round-trip, naming examples from requirement 002
  (`Invoice`→`invoices`, `CreateInvoice`→`POST /api/invoices`, `GetInvoiceById`→
  `GET /api/invoices/{id}`, `GetAllInvoices`→`GET /api/invoices`), YAML round-tripping.
- **Template.Tests:** for each architecture profile × adapter (4 cells, in-memory persistence
  varied where relevant): slice file sets per operation (Create/Update/Delete/GetById/GetAll),
  validator presence rules, mapping files, Result-pattern foundation files, Program.cs required
  shape (extension calls present, raw registrations absent), middleware/DI extension files,
  Contracts placement (clean arch) vs slice-local DTOs (vertical slice), layer-leak checks
  (dispatcher/persistence types absent from Domain output paths).
- **E2E:** full 10-combination vertical-slice matrix (existing) + clean-architecture combinations
  covering both adapters and all persistence providers at least once; ordering error paths through
  the real CLI; protected-region regeneration (existing, updated); one representative combination
  runs `dotnet test` on the generated solution. CI smoke matrix (20 jobs) continues to prove every
  adapter × persistence × architecture cell scaffolds/generates/builds; representative job also
  runs the generated test suite. No new CI axis needed.
- **Manual:** curl CRUD against one running generated API to confirm status-code mapping
  (201/200/204/404).

## Out of scope

- `Directory.Packages.props` / central package management in generated solutions.
- Automatic audit-field population (CreatedBy/UpdatedAt writes) in generated handler bodies.
- MediatR `IPipelineBehavior`-based validation (validation stays at the endpoint seam, shared by
  both adapters; handlers still never see invalid requests).
- Entity-level `InvoiceDto` shared contract type (Request/Response records cover the API surface).
- Pagination wiring for GetAll endpoints (`PagedResult<T>` type ships; GetAll keeps returning the
  full list until a paging requirement lands).
- EF Core migrations, `adapter switch`, publishing the tool — unchanged roadmap items.

## Outcome (fill in at delivery)

- What shipped, with commit hashes.
- Deviations from the plan above and why.
- Follow-ups discovered (add real ones to `docs/ROADMAP.md`).
