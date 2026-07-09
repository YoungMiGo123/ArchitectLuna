# Architecture

## Pipeline

```
.architect/model.yaml → ArchitectModel (IR) → validate → IFrameworkAdapter + IPersistenceGenerator
  → Scriban render → protected-region merge against existing files → write → .architect/manifest.json
```

1. **Intent Model** — `.architect/model.yaml`, a small YAML document: solution name, root
   namespace, chosen adapter, and a list of features. Each feature holds entities, commands, and
   queries. It is hand-edited indirectly, through the `add feature` / `add entity` / `add crud` /
   `add command` / `add query` CLI commands — see `ArchitectLuna.Core/Model/*.cs`. All mutations
   go through `ModelEditor` (`ArchitectLuna.Core/Editing/ModelEditor.cs`), which owns the
   ordering/duplicate rules (feature before entity, entity before CRUD, bespoke commands/queries
   allowed without an entity, duplicates rejected without partial mutation) so the CLI and UI are
   thin presenters and the rules are unit-testable in one place.
2. **Entity → CRUD synthesis** — `add entity` is the primary way commands/queries get created.
   `CrudSynthesizer` (`ArchitectLuna.Core/Model/CrudSynthesizer.cs`) expands one `EntityModel`
   into the standard Create/Update/Delete commands and GetById/GetAll queries, so the model is
   built outward from entities rather than assembled command-by-command. `add crud` re-synthesizes
   missing operations for an existing entity; `add command`/`add query` remain available for
   anything a standard CRUD shape doesn't cover.
3. **Validation** — `ModelValidator` checks the model before generation (known adapter, no
   duplicate feature/command/query/field names) and fails fast with a readable error list.
4. **Adapter dispatch** — `IFrameworkAdapter` (`ArchitectLuna.Core/Generation/IFrameworkAdapter.cs`)
   is the single seam between "what the model says" and "what code gets written." `MediatRAdapter`
   and `WolverineAdapter` both implement it. Given a `FeatureModel` + `CommandModel`/`QueryModel`,
   an adapter returns a list of `GeneratedFile` (relative path + rendered content) — it does no
   file I/O itself. Per operation, an adapter emits the message record, the `{Op}Result` record,
   the handler (returning `Result<{Op}Result>`), the FluentValidation validator (body-carrying
   commands only), `{Op}Request`/`{Op}Response` DTOs (to the Contracts target), explicit
   extension-method mappings (`{Op}Mappings`), and the endpoint. Endpoints translate a failed
   result via the scaffolded `ResultHttpExtensions.ToProblem()` and map success to 201 (Create),
   200 (Update/queries), or 204 (Delete).
5. **Route inference** — `RouteInference` (`ArchitectLuna.Core/Naming/RouteInference.cs`) is
   shared by both adapters so the same model always produces the same route shape regardless of
   `--adapter`: `POST /api/{feature}` for Create, `PUT`/`DELETE /api/{feature}/{id}` for
   Update/Delete, `GET /api/{feature}/{id}` for a single-id-param query, `GET /api/{feature}` for
   a zero-param (list) query.
6. **Templates** — `.sbn` (Scriban) files embedded as resources in `ArchitectLuna.Templates`.
   `Templates/MediatR` and `Templates/Wolverine` hold the framework-specific pieces (the message
   record's base interface, the handler shape); `Templates/Shared` holds the command/query
   endpoint and the FluentValidation validator — both adapters render endpoints through the exact
   same templates, so the generated HTTP surface (minimal-API `IEndpointDefinition` mapping, verbs,
   route binding) is identical no matter which adapter produced it. Only the injected dispatcher
   (`ISender` vs `IMessageBus`) and the handler internals differ.
7. **Protected-region merge** — `ProtectedRegionMerger` scans an existing file for
   `// <architect:region name="...">...// </architect:region>` blocks and splices their content
   into the freshly rendered file before writing, so hand-written logic inside a handler body
   survives regeneration even as the surrounding scaffolding (usings, class name, signature)
   stays in sync with the model.
8. **Persistence binding** — before rendering a command/query's Handler file, the adapter asks the
   configured `IPersistenceGenerator` (`ArchitectLuna.Core/Generation/IPersistenceGenerator.cs`)
   for a `HandlerBinding`: the body statements plus at most one dependency to inject (a DbContext,
   an `IDocumentSession`, etc — see `HandlerBinding.cs`'s doc comment for why it's capped at one).
   `NullPersistenceGenerator` (selected by `--persistence none`) returns the original
   `throw new NotImplementedException();` placeholder unchanged, so that choice stays fully
   backward compatible; `new api` defaults to `--persistence in-memory` instead, so a freshly
   scaffolded solution has real handler bodies out of the box. A provider also gets two
   file-generation hooks: `GenerateEntityPersistence`
   (once per entity — a domain class, an EF `IEntityTypeConfiguration<T>`, etc.) and
   `GenerateSolutionPersistence` (once per `generate` run, given every entity across every
   feature — for a DbContext that needs one `DbSet<T>` per entity; providers with nothing
   solution-level to emit, like Marten, return an empty list).
9. **Write + manifest** — `FileWriter` performs the merge-then-write for each `GeneratedFile` and
   records every path ever generated in `.architect/manifest.json`, laying groundwork for future
   cleanup tooling (e.g. detecting files that used to be generated but no longer are).

## Layout: `GenerationContext` and the vertical-slice/Clean-Architecture split

`GenerationContext` (`ArchitectLuna.Core/Generation/GenerationContext.cs`) is the seam that lets one
model produce either output shape. It carries five independent `ProjectTarget`s (project root path +
namespace) — `Api`, `Application`, `Domain`, `Infrastructure`, `Contracts` — instead of a single
project root. Adapters and persistence generators never branch on layout directly; they always ask
"where does the Application target live" or "where does the Contracts target live" and get the
right answer:

- **`GenerationContext.ForVerticalSlice`** collapses everything into one physical project:
  `Api`/`Application`/`Contracts` all resolve to the Api project root (so Request/Response DTOs
  stay inside their feature slice); `Domain`/`Infrastructure` both resolve to a `Persistence`
  sub-namespace/folder inside it.
- **`GenerationContext.ForCleanArchitecture`** points each target at a genuinely separate project
  (`src/{Solution}.Api`, `.Application`, `.Domain`, `.Infrastructure`, `.Contracts`), dependency
  rule pointing inward: entities go to Domain, messages/handlers/validators/mappings to
  Application, Request/Response DTOs to Contracts (referenced by Application and Api, referencing
  nothing), EF configs/DbContext to Infrastructure, endpoints stay in Api.

`GenerationContext.HasSeparateInfrastructure` (true only for Clean Architecture) is the signal EF
Core's persistence generator uses to avoid an illegal `Application → Infrastructure` project
reference: when true, it emits an `I{Solution}DbContext` interface *in Application* (owning the
abstraction, per the Dependency Inversion Principle) with a `DbSet<T>` per entity, and the concrete
`DbContext` *in Infrastructure* implements it. Handlers depend on the interface; the generated
`AddInfrastructure` extension wires the concrete type to it with
`AddScoped<IFooDbContext>(sp => sp.GetRequiredService<FooDbContext>())`. For vertical slice, where
there's only one project anyway, this indirection is skipped entirely — handlers depend on the
concrete `DbContext` directly, exactly as before this abstraction existed.

One consequence worth knowing: a **freshly scaffolded solution must compile before the first
`generate` run**, but the generated `AddInfrastructure` already references the DbContext type as
soon as EF Core persistence is configured. `SolutionScaffolder` handles this by calling
`IPersistenceGenerator.GenerateSolutionPersistence` at scaffold time too, with zero entities — EF
Core's implementation always emits the DbContext (with zero `DbSet`s, and no `using` for an
`Entities` namespace nothing has populated yet, which would otherwise be a compile error) rather
than early-returning when there's nothing to generate yet. `generate` re-renders it with real
`DbSet`s once entities exist.

`ArchitectLuna.Cli/Scaffolding` mirrors this split: `SolutionScaffolder` orchestrates either
`ScaffoldVerticalSlice` or `ScaffoldCleanArchitecture`; `ProjectFiles`, `ProgramCsBuilder`,
`FoundationFiles`, `InfrastructureFiles`, and `TestProjectScaffolder` hold the shared,
layout-agnostic pieces (csproj/`Directory.Build.props` content, `Program.cs`, the production
foundation, Dockerfile/docker-compose/appsettings/launchSettings/docs/.editorconfig, xUnit test
projects) so neither layout duplicates them.

## The production foundation and the clean-startup contract

`FoundationFiles` (`ArchitectLuna.Cli/Scaffolding/FoundationFiles.cs`) builds every scaffold-time
foundation file as a pure function from `GenerationContext` (+ adapter/persistence choice) to
`GeneratedFile` records — which is what lets `ArchitectLuna.Template.Tests` verify the whole
foundation with zero file/process I/O. It emits, per project target: the Result pattern
(`Result`/`Result<T>`/`Error`+`ErrorType`/`ValidationError`/`PagedResult<T>`) and the
`IUserContext`/`IDateTimeProvider` abstractions into Application; `BaseEntity` into Domain;
`SystemDateTimeProvider` and the `AddInfrastructure` extension into Infrastructure; and the HTTP
concerns into Api (`IEndpointDefinition`, exception + correlation-ID middleware,
`ResultHttpExtensions.ToProblem()`, `HttpUserContext`, and the `AddApi`/`UseApiMiddleware`/
`MapApiEndpoints`/`UseApiLogging` extensions).

`Program.cs` (built by `ProgramCsBuilder`) is only ever the required clean shape — `UseApiLogging`,
`AddApi`/`AddApplication`/`AddInfrastructure`, `UseApiMiddleware`, `MapApiEndpoints` — and is
scaffold-time-only, never regenerated per feature. The one adapter-specific deviation: Wolverine's
handler discovery is an `IHostBuilder` concern, so Wolverine solutions carry one extra
`builder.Host.UseWolverine(opts => { … })` block. Two settings matter there:
`opts.UseRuntimeCompilation()` (core WolverineFx stopped shipping the runtime compiler, so without
it — and the `WolverineFx.RuntimeCompilation` package the adapter requires — a generated app
compiles fine but throws at startup) and `opts.ServiceLocationPolicy = ServiceLocationPolicy.
AlwaysAllowed` (Wolverine 6 otherwise refuses to service-locate a handler's injected
`DbContext`/`IDocumentSession` from the DI scope and throws at the first message — this keeps the
messaging and persistence seams orthogonal without an integration package).

## Persistence registration, schema, and health (the `AddPersistence` seam)

`Program.cs`/`AddInfrastructure` never contain per-provider registration. Instead each
`IPersistenceGenerator` emits a `PersistenceRegistration.cs` — a static
`AddPersistence(this IServiceCollection, IConfiguration)` — from `GenerateSolutionPersistence`,
regenerated on every `generate` with the full entity list (and emitted at scaffold time with zero
entities, like the DbContext). The foundation-owned `AddInfrastructure` just calls
`services.AddPersistence(configuration)`. Giving the provider whole-model visibility at
registration time is what lets **Marten** register each generated document type
(`RegisterDocumentType<T>`) and apply schema at startup (`ApplyAllDatabaseChangesOnStartup()`), and
lets **EF Core** register a `DatabaseInitializer` hosted service (migrate-if-migrations-exist-else-
`EnsureCreated`) plus a `DatabaseHealthCheck` — so a generated `efcore-*`/`marten` solution creates
its own schema and serves real CRUD against a live database with no manual migration step. EF Core
deliberately does **not** scaffold `Microsoft.EntityFrameworkCore.Design`: as a `PrivateAssets=all`
dev dependency it pins EF Core's `Relational` assembly to Microsoft's latest patch while the Npgsql
provider tracks its own, and the private version never reaches the startup project's runtime output
— an app that compiles but throws a `Relational` `FileNotFoundException` at boot; migrations remain
an opt-in step (add Design + `dotnet ef migrations add`). Health is split by `EndpointExtensions`
into `/health` (liveness) and `/health/ready` (readiness — the DB-tagged checks).

Synthesized `GetAll` is **paged**: `CrudSynthesizer` gives it `Page`/`PageSize` params
(`QueryModel.IsPaged`), `RouteInference` keeps it on the plain collection route (page/pageSize bind
from the query string), the adapters render `Result<PagedResult<T>>` and a `PagedResult<Response>`
endpoint, and every provider's GetAll binding does `Skip`/`Take` + a total `Count`.

## Component map

| Project | Responsibility |
|---|---|
| `ArchitectLuna.Core` | Intent Model, `ModelEditor` (ordering/duplicate rules for all model mutations), naming/route inference, validation, protected-region merge, manifest, `IFrameworkAdapter`/`IPersistenceGenerator` contracts. No knowledge of MediatR/Wolverine/EF Core/Marten/Scriban. |
| `ArchitectLuna.Templates` | Scriban engine wrapper + embedded `.sbn` templates. No knowledge of the CLI or the Intent Model's YAML shape. |
| `ArchitectLuna.Adapters.MediatR` / `ArchitectLuna.Adapters.Wolverine` | Implement `IFrameworkAdapter`; each depends only on `Core` and `Templates`, not on each other, so a third-party adapter can be added the same way without touching existing ones. |
| `ArchitectLuna.Persistence.InMemory` / `ArchitectLuna.Persistence.EfCore` / `ArchitectLuna.Persistence.Marten` | Implement `IPersistenceGenerator`; same independence property as the messaging adapters — a provider only ever adds new files, it never needs to modify another provider or adapter. InMemory is the `new api` default: zero NuGet packages, zero external process, real CRUD backed by a generated in-process store. |
| `ArchitectLuna.Ui` | Razor Pages app built directly on `Core` (no CLI dependency for model reads/writes, confirming Core's zero-console-I/O boundary is real) plus a runtime-only shell-out to the built CLI for `generate`. |
| `ArchitectLuna.Cli` | Spectre.Console.Cli entry point (`new`, `add feature/entity/crud/command/query`, `generate`) plus `Scaffolding/` (`SolutionScaffolder` + `ProjectFiles`/`ProgramCsBuilder`/`FoundationFiles`/`InfrastructureFiles`/`TestProjectScaffolder`), which shells out to the real `dotnet` CLI for `.sln`/project creation and package references so version resolution always comes from the live NuGet feed. `AdapterRegistry`/`PersistenceRegistry` resolve `--adapter`/`--persistence` strings to concrete implementations — this is the one place that knows about every adapter and provider by name. |

## Why Scriban, and a known environment gotcha

Templates are authored in [Scriban](https://github.com/scriban/scriban), a sandboxed templating
language with no ambient file/network access — the only way a template can affect its output is
through the view model it's given. `TemplateEngine` renders with `StandardMemberRenamer`, so a
C# `CommandName` property is referenced in a template as `{{ command_name }}`.

One gotcha worth knowing if you add new `.sbn` files: MSBuild infers satellite-assembly cultures
from filenames, and a file like `Handler.cs.sbn` gets misread as culture `cs` (Czech) — the
resource silently ends up in a separate satellite assembly instead of the main one, and
`GetManifestResourceNames()` on the main assembly returns nothing at runtime. The fix, already
applied in `ArchitectLuna.Templates.csproj`, is `WithCulture="false"` on the `EmbeddedResource`
item.

## Why shell out to `dotnet` for scaffolding

`new api` calls `dotnet new sln`, `dotnet sln add`, and `dotnet add package` as real subprocesses
rather than hand-writing `.sln`/`.csproj` XML with pinned package versions. This means a scaffolded
solution always resolves current NuGet versions and always has a `.sln`/`.slnx` MSBuild itself
considers valid, at the cost of requiring the `dotnet` CLI to be on `PATH` when running
`architect-luna new api`.

## Two more gotchas, found while wiring up persistence

- **A handler with a persistence-backed body must be `async`.** Both `Handler.cs.sbn` templates
  declare `Handle` as `async Task<T>` unconditionally (not just when persistence is configured),
  because a `HandlerBinding` body uses `await`/`SaveChangesAsync`, and a non-`async` method can't
  contain `await`. The `--persistence none` placeholder (`throw new NotImplementedException();`)
  still compiles fine inside an `async` method — it just never hits an `await`.
- **Wolverine's static `Handle` method must declare `CancellationToken cancellationToken`
  explicitly.** Unlike MediatR (which always passes one), Wolverine only supplies parameters a
  handler method actually declares — omitting it compiles for a `--persistence none` handler
  (nothing references it) and only fails once a `HandlerBinding` body starts calling
  `SaveChangesAsync(cancellationToken)`. Both `Handler.cs.sbn` templates now declare it
  unconditionally so this can't regress silently for one adapter while the other is fine.
