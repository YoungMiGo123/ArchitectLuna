# Roadmap

## Done

- **M1 — `new api` skeleton scaffolding.** Shells out to real `dotnet new sln`/`sln add`/`add
  package` so the scaffolded solution always resolves live NuGet versions and has a valid
  `.sln`/`.slnx`.
- **M2 — Intent Model + `add feature`/`add command`/`add query` + `generate`.** YAML model,
  Scriban rendering, protected-region-aware file writer, manifest.
- **M2.5 — `IFrameworkAdapter` abstraction + Wolverine adapter.** Both adapters share route
  inference and the endpoint/validator templates; only message/handler shape and dispatch differ.
- **M3 — Manifest + protected regions**, verified with a real hand-edit-then-regenerate round trip.
- **M4 — Compiling end to end.** Every adapter verified by actually scaffolding, generating, and
  `dotnet build`-ing a sample solution — automated in `ArchitectLuna.EndToEnd.Tests`, not just
  eyeballed once by hand.
- **Entity-driven CRUD.** `add entity` synthesizes Create/Update/Delete + GetById/GetAll from one
  entity definition (`CrudSynthesizer`), with real REST verbs/routes (`POST`/`PUT /{id}`/`DELETE
  /{id}`/`GET /{id}`/`GET` collection) and GetById/GetAll returning actual entity data instead of
  echoing the lookup key.
- **Entities → real persistence.** `IPersistenceGenerator` is the seam (`Core/Generation/
  IPersistenceGenerator.cs`, `HandlerBinding.cs`): a messaging adapter asks the configured provider
  for a handler's body plus one injected dependency. Three providers exist — `efcore-postgres` and
  `efcore-sqlserver` (`ArchitectLuna.Persistence.EfCore`, one implementation parameterized by
  provider kind: domain entity class + `IEntityTypeConfiguration<T>` per entity, one `DbContext`
  with a `DbSet<T>` per entity) and `marten` (`ArchitectLuna.Persistence.Marten`: a plain document
  class per entity, `IDocumentSession` Store/Load/Delete/Query, no DbContext-equivalent needed).
  Verified for every adapter × persistence combination.
- **CI pipeline.** `.github/workflows/ci.yml`: build+test on every push/PR (no branch filter), plus
  a smoke-test matrix that scaffolds, generates, and builds a real solution for every
  adapter × persistence combination.
- **A UI layer directly over `ArchitectLuna.Core`.** `ArchitectLuna.Ui` (Razor Pages): read-only
  model viewer, an add-entity form using Core directly (no CLI shell-out for model edits), and a
  generate button that shells out to the built CLI. Confirms Core's zero-console-I/O boundary is
  real, not aspirational — the UI never needed to touch `ArchitectLuna.Cli`'s own code.
- **Package `architect-luna` as a real `dotnet tool`.** `ArchitectLuna.Cli.csproj` has
  `PackAsTool`/`ToolCommandName`/`PackageId`/`Version`/`PackageReadmeFile` set; `dotnet pack` +
  `dotnet tool install --global --add-source ./nupkg architect-luna` verified end to end — bare
  `architect-luna` command, run from an arbitrary directory, scaffolds and generates a real
  buildable solution. See README's "Installing" section. Publishing to a real feed is now
  automated — see the "Automated publish to a real feed" entry below.
- **A zero-setup `in-memory` persistence provider, made the `new api` default.**
  `ArchitectLuna.Persistence.InMemory` (`InMemoryPersistenceGenerator`) implements
  `IPersistenceGenerator` exactly like EF Core/Marten, but needs no NuGet package and no external
  process: one generated `InMemoryStore` (a `ConcurrentDictionary` keyed by entity type + id,
  registered as a DI singleton) backs every entity's Create/Update/Delete/GetById/GetAll. This
  closes the gap where a freshly scaffolded solution's handlers all threw
  `NotImplementedException` unless you separately stood up Postgres/SQL Server — now `new api`
  with no flags produces a solution that builds *and* serves real CRUD immediately (verified by
  running the generated API and curling Create/GetById/GetAll/Update/Delete against it, not just
  `dotnet build`). `--persistence none` still exists as an explicit opt-out for
  placeholder-only handlers. Covered by `ArchitectLuna.EndToEnd.Tests` and the CI smoke matrix
  for both adapters; that same pass also added the previously-missing `marten` cases to
  `GeneratedSolutionBuildTests` (it was in the CI smoke matrix but absent from the xUnit suite).
- **Clean Architecture layering as an alternative to vertical slice.** `--architecture
  clean-architecture` (default remains `vertical-slice`) splits generated code across four real
  projects — Domain/Application/Infrastructure/Api — sharing the same Intent Model, adapters, and
  persistence providers as vertical slice. `GenerationContext` carries four independent
  `ProjectTarget`s so adapters/persistence generators never branch on layout (see
  `docs/ARCHITECTURE.md`'s "Layout" section); EF Core's persistence generator emits an
  `I{Solution}DbContext` interface in Application (implemented by the concrete `DbContext` in
  Infrastructure) so the dependency rule — Application never references Infrastructure — actually
  holds, not just by convention. Verified end to end for every adapter × persistence combination in
  both layouts (`CleanArchitectureBuildTests`), including that a fresh scaffold compiles *before*
  the first `generate` run.
- **Production-readiness baseline, every scaffold.** Swagger (`Swashbuckle.AspNetCore`), health
  checks at `/health`, a global `ExceptionHandlingMiddleware` (maps `KeyNotFoundException` → 404,
  anything else → 500 as a `problem+json` body), Serilog console logging, and an xUnit test project
  (`{Solution}.Api.Tests`, plus `{Solution}.Application.Tests` for Clean Architecture) are part of
  every `new api` scaffold now, not a follow-up step.
- **Docker, every scaffold.** A multi-stage `Dockerfile` (restores/publishes the whole solution,
  runs just the Api project) and a `docker-compose.yml` — with a `db` service (Postgres or SQL
  Server, matching `--persistence`) wired via `ConnectionStrings__Default` when persistence is
  configured — plus `Properties/launchSettings.json` and an `appsettings.json`/
  `appsettings.Development.json` split (base file has no secrets; the dev connection string lives
  only in the gitignorable Development file).
- **Automated publish to a real feed.** `.github/workflows/publish-nuget.yml` packs
  `architect-luna` and pushes it to the team's Azure Artifacts feed (`Nuget-Packages`,
  `LoadshednomoConsulting` org) on every push to `master`, with auto-generated versions
  (`<VERSION_PREFIX>.<run number>`, patch = workflow run number) and a build + fast-test gate.
  Needs a one-time `AZURE_ARTIFACTS_PAT` repo secret (Azure DevOps PAT, Packaging Read & Write) —
  setup documented at the top of the workflow file; consumer install documented in README's
  "Installing" section.

- **Production foundation + three-tier testing layer** (plan:
  `docs/plans/001-production-foundation-and-testing-layer.md`; requirements:
  `docs/requirements/001-implementation-architecture.md` + `002-testing-layer.md`). Every scaffold
  now ships the Result pattern (`Result`/`Result<T>`/`Error`/`ValidationError`/`PagedResult<T>` —
  handlers return results, endpoints map them to 201/200/204 + 400/404/409/401/403/500 via one
  `ToProblem()` extension), `BaseEntity` (inherited by every generated entity/document),
  `IUserContext`/`IDateTimeProvider` abstractions with HTTP/system implementations, correlation-ID
  + exception middleware, Serilog request logging, and a Request/Response DTO + extension-method
  mapping layer per slice. Startup is the clean extension shape (`UseApiLogging`, `AddApi`/
  `AddApplication`/`AddInfrastructure`, `UseApiMiddleware`, `MapApiEndpoints`) built by
  `FoundationFiles`/`ProgramCsBuilder`; `IPersistenceGenerator` registration moved from Program.cs
  splicing to the generated `AddInfrastructure`. Clean Architecture gained a fifth `Contracts`
  project (and became the `new api` default per the requirement doc); ordering rules moved to
  `Core/Editing/ModelEditor` with a new `add crud` verb. Testing is now three tiers: categorized
  `Core.Tests`, an in-memory `Template.Tests` snapshot project (foundation presence, Program.cs
  shape, slice file sets, layer-leak checks — milliseconds, no I/O), and an expanded E2E suite
  (ordering error paths through the real CLI, widened clean-architecture matrix, and a `dotnet
  test` run of a generated solution's own test suite — which immediately caught that core
  WolverineFx stopped shipping its runtime compiler: generated Wolverine apps compiled but threw
  at startup; fixed via `WolverineFx.RuntimeCompilation` + `opts.UseRuntimeCompilation()`).

- **Pagination for generated GetAll queries** (plan: `docs/plans/002-crud-getall-pagination.md`).
  Every CRUD-synthesized `GetAll{Entity}` query now accepts `page`/`pageSize` query-string
  parameters and returns `Result<PagedResult<T>>` — `PagedResult<T>` existed since the production
  foundation but was unused dead code until now. All three persistence providers page with
  `Skip`/`Take` (or the Marten/EF Core equivalent) instead of loading the entire table, ordered by
  `Id` for deterministic paging; missing page/pageSize default to page 1 / 20 items. Works
  identically across `mediatr`/`wolverine` and both `vertical-slice`/`clean-architecture` layouts —
  Page/PageSize never join `QueryModel.Params`, so the route stays the plain collection route
  (`GET /api/{feature}`) with no `RouteInference` changes. Out of scope: hand-authored
  `add query --collection` queries (not CRUD-synthesized) are still unbounded; a typed Contracts
  `PagedResponse<T>` DTO (currently an anonymous object) is a follow-up.

- **Generation quality, entity sync, and database readiness** (plan:
  `docs/plans/003-generation-quality-and-db-readiness.md`; requirements:
  `docs/requirements/003-improvements.md`). `add field`/`update entity --add-field` add a field to
  an existing entity and resync every dependent artifact (persistence config, commands, queries,
  validators, mappings, handlers) through the same pipeline `generate` uses; `sync entity` and
  `config set database.applyMode` round out the CLI surface. Field-type-based default validation
  (`NotEmpty`/`GreaterThan(0)`/`EmailAddress`/etc.) now layers under explicit `--rule` entries
  instead of validators being 100% explicit-rule-only. Generated output is auto-formatted with
  `dotnet format` after `generate`/`new api` (`--no-format` to skip). The exception middleware maps
  `DbUpdateConcurrencyException` to 409 and `DbUpdateException` to a logged 500. EF Core solutions
  ship `Microsoft.EntityFrameworkCore.Design` and a design-time `DbContext` factory, so `dotnet ef
  migrations add`/`database update` work out of the box (closing the formerly-tracked "EF Core
  migrations" near-term item). A new `database.applyMode` (`manual`/`on-generate`/`on-startup`) setting drives EF Core
  startup migration/`on-generate` `dotnet ef database update` and Marten's
  `AutoCreateSchemaObjects`. Docker/Compose gained health checks. The separate `Contracts` project
  is gone — Request/Response DTOs now live in a `Contracts/` subfolder of each Application feature
  slice in both layouts. `add entity`/`add crud` offer to create a missing feature
  (`--yes`/`--create-missing` for non-interactive use) instead of just failing. The generated
  README documents all of the above. Verified via the Core/Template snapshot tiers (198 tests);
  the full E2E adapter × persistence × layout matrix needs to be re-run/confirmed in CI — this
  session's sandbox network made `dotnet add package`-heavy E2E runs impractically slow to
  complete locally (see the plan's Outcome section).

## Near-term — get to a demoable prototype

- **`invero doctor` / `--verify`.** Run `dotnet build` after `generate` and map errors back to the
  offending `model.yaml` entry, so a bad field type or a naming collision surfaces immediately
  instead of at the next manual build.
- **`adapter switch`.** Regenerate an existing model onto a different adapter in place — the
  proof that `IFrameworkAdapter` is a real seam, not just a naming convention.
- **Grouped/split operation-layout mode** (docs/requirements/003-improvements.md §3.3). Command +
  handler + result in one file is now the (already-shipped) default; the `split` alternative that
  breaks them into separate files was descoped from plan 003.
- **Marten `on-generate` apply mode.** Currently behaves like `manual` — there's no CLI-side
  equivalent to `dotnet ef database update` for Marten without the generator itself connecting to
  a live database, which plan 003 deliberately didn't take on.
- **UI: `--rule` support in the add-entity form.** The CLI's `add entity --rule Field:RuleExpr`
  has no UI equivalent yet (self-disclosed gap from the UI build) — only Name/Type field rows.
- **UI: `add field`, `config set`, and compound-command support.** New in plan 003; the UI's
  add-entity form doesn't yet cover any of it.

## Medium-term

- **`SchemaVersion` migration.** `ArchitectModel.SchemaVersion` exists but nothing reads it yet;
  once the YAML shape needs to change, this is what upgrades an older `model.yaml` in place.

## Longer-term

- **FastEndpoints adapter**, then a non-.NET adapter (NestJS or Spring) as the real test of
  whether `IFrameworkAdapter`'s language-agnostic parts of the design (route inference, CRUD
  synthesis) hold up outside .NET.
