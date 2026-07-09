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
  `Id` for deterministic paging; missing `page` defaults to 1, missing/oversized `pageSize`
  defaults to and caps at 20/100 respectively (a resource-exhaustion guard added while merging
  with plan 003 below). Works identically across `mediatr`/`wolverine` and both
  `vertical-slice`/`clean-architecture` layouts — Page/PageSize never join `QueryModel.Params`, so
  the route stays the plain collection route (`GET /api/{feature}`) with no `RouteInference`
  changes. Out of scope: hand-authored `add query --collection` queries (not CRUD-synthesized) are
  still unbounded; a typed Contracts `PagedResponse<T>` DTO (currently an anonymous object) is a
  follow-up. *(Closed — see the standard response envelope entry below: `GetAll` now returns a
  typed `PagedResponse<T>`.)*
- **Runnable persistence + schema init + production hardening** (plan:
  `docs/plans/003-runnable-persistence-and-schema-init.md`, developed in parallel with plan 002
  above and merged alongside it). Generated `efcore-postgres`/`efcore-sqlserver`/`marten`
  solutions now create their own schema at startup and serve real CRUD against a live database
  with no manual migration step — verified end to end against a live Postgres for MediatR+EF Core,
  Wolverine+Marten, and Wolverine+EF Core, not just `dotnet build`. Each provider emits an
  `AddPersistence` extension (regenerated per `generate` with full entity knowledge, replacing the
  old scaffold-time `BuildServiceRegistration`): EF Core adds a `DatabaseInitializer`
  (migrate-else-`EnsureCreated`) + `DatabaseHealthCheck` + connection resilience; Marten registers
  every document type + `ApplyAllDatabaseChangesOnStartup()` + a health check. `/health` (liveness)
  and `/health/ready` (DB readiness) are split. Wolverine handlers set
  `ServiceLocationPolicy.AlwaysAllowed` so they resolve the injected
  `DbContext`/`IDocumentSession` (Wolverine 6 otherwise throws at the first message). EF Core's
  `Design` package is deliberately not scaffolded (its `PrivateAssets=all` split the `Relational`
  assembly version between compile and runtime and threw at startup); migrations are a documented
  opt-in.
- **Standard `ApiResponse<T>` response envelope + Controller output** (plan:
  `docs/plans/004-standard-response-envelope-and-controllers.md`, requirements:
  `docs/requirements/004-standards-return-types.md`). Every generated API response is now wrapped
  in `{ success, payload, error }` regardless of adapter (MediatR/Wolverine), persistence provider
  (EF Core/Marten/in-memory/none), or `--api-style`. Centralized
  `src/{Solution}.Api/Results/ResultExtensions.cs` (`ToOkResponse`/`ToCreatedResponse`/
  `ToNoContentResponse`/`ToErrorResponse`/`ToValidationErrorResponse`) replaces per-endpoint
  `Results.Ok`/`Results.Created`/`Results.Problem`/`Results.ValidationProblem` calls; OpenAPI
  metadata documents `ApiResponse<T>`, not the raw response DTO. `GetAll` returns a typed
  `PagedResponse<T>` (`src/{Solution}.Contracts/Common/PagedResponse.cs`) instead of an anonymous
  paging object. New `--api-style controllers` (default remains `minimal-api`) generates
  `[ApiController]` actions instead of `IEndpointDefinition` Minimal API classes, via a parallel
  `ResultActionExtensions` (`IActionResult`-returning) sharing the same envelope/status-code
  mapping — the two styles are contractually identical over HTTP. `ArchitectLuna.Ui` has no
  API-style picker yet (gap).

## Near-term — get to a demoable prototype

- **`invero doctor` / `--verify`.** Run `dotnet build` after `generate` and map errors back to the
  offending `model.yaml` entry, so a bad field type or a naming collision surfaces immediately
  instead of at the next manual build.
- **`adapter switch`.** Regenerate an existing model onto a different adapter in place — the
  proof that `IFrameworkAdapter` is a real seam, not just a naming convention.
- **EF Core migrations, first-class.** *(Runnability is done — see plan 003: a generated
  `efcore-*` app now creates its schema at startup via the `DatabaseInitializer` and is runnable
  immediately; migrations are a documented opt-in.)* Remaining: scaffold an initial migration and
  wire `dotnet ef migrations add`/`database update` into the tool so teams get the
  migration-tracked path without hand-adding the `Design` package — the initializer already
  prefers migrations over `EnsureCreated` when any exist.
- **UI: `--rule` support in the add-entity form.** The CLI's `add entity --rule Field:RuleExpr`
  has no UI equivalent yet (self-disclosed gap from the UI build) — only Name/Type field rows.

## Medium-term

- **`SchemaVersion` migration.** `ArchitectModel.SchemaVersion` exists but nothing reads it yet;
  once the YAML shape needs to change, this is what upgrades an older `model.yaml` in place.

## Longer-term

- **FastEndpoints adapter**, then a non-.NET adapter (NestJS or Spring) as the real test of
  whether `IFrameworkAdapter`'s language-agnostic parts of the design (route inference, CRUD
  synthesis) hold up outside .NET.
