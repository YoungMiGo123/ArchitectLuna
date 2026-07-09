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
  buildable solution. See README's "Installing" section (now also documents publishing to GitHub
  Packages/NuGet.org so teammates/CI can install without a checkout — the actual `dotnet nuget
  push` still hasn't been run against a real feed, so "verified end to end" only covers the local
  `--add-source ./nupkg` path).
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

## Near-term — get to a demoable prototype

- **`invero doctor` / `--verify`.** Run `dotnet build` after `generate` and map errors back to the
  offending `model.yaml` entry, so a bad field type or a naming collision surfaces immediately
  instead of at the next manual build.
- **`adapter switch`.** Regenerate an existing model onto a different adapter in place — the
  proof that `IFrameworkAdapter` is a real seam, not just a naming convention.
- **EF Core migrations.** `dotnet ef migrations add`/`database update` wired into `new api`/
  `generate` (or documented as a manual follow-up step) — right now a generated `DbContext`
  compiles but nothing creates the schema. (The new `in-memory` default sidesteps this for a
  first run, but `efcore-postgres`/`efcore-sqlserver` still need it before they're runnable.)
- **UI: `--rule` support in the add-entity form.** The CLI's `add entity --rule Field:RuleExpr`
  has no UI equivalent yet (self-disclosed gap from the UI build) — only Name/Type field rows.

## Medium-term

- **`SchemaVersion` migration.** `ArchitectModel.SchemaVersion` exists but nothing reads it yet;
  once the YAML shape needs to change, this is what upgrades an older `model.yaml` in place.
- **Publish the packaged tool to a real feed.** GitHub Packages (natural fit, repo's already
  there) or NuGet.org, so `dotnet tool install --global architect-luna` works for anyone without
  a source checkout — see README's "Installing" section for the manual/local-feed version that
  already works today.

## Longer-term

- **FastEndpoints adapter**, then a non-.NET adapter (NestJS or Spring) as the real test of
  whether `IFrameworkAdapter`'s language-agnostic parts of the design (route inference, CRUD
  synthesis) hold up outside .NET.
