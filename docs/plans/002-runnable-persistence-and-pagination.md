# Plan 002: Runnable persistence + pagination + production hardening

- **Status:** In progress
- **Complexity:** L
- **Author:** Claude (claude-opus-4-8 session)
- **Date started:** 2026-07-09
- **Checklist used:** none single-fits; `new-persistence-provider.md` discipline applied to the
  per-provider registration/schema work.

## Summary

Today a generated `efcore-*` solution compiles and boots but **cannot serve a request** — the
DbContext has no schema behind it (no migration, no `EnsureCreated`, no Design tooling), and
`marten` never registers its document types or applies schema, so its tables are never created.
`GetAll` returns the entire table with no paging even though `PagedResult<T>` already ships. This
plan makes every persistence provider *actually runnable and production-shaped* out of the box —
schema created at startup, database readiness health checks, connection resilience — and wires
real pagination through `GetAll` across both adapters and all four persistence providers.

## Affected components

| Component | Change |
|---|---|
| `ArchitectLuna.Core` | `QueryModel.IsPaged`; `CrudSynthesizer` marks `GetAll` paged + adds `Page`/`PageSize`; `RouteInference` unchanged (paged GetAll stays the collection route, page/pageSize bind from query string); `IPersistenceGenerator` drops `BuildServiceRegistration`/`ServiceRegistrationUsings` in favour of an `AddPersistence` file the provider emits from `GenerateSolutionPersistence` (so registration has full entity knowledge, regenerated per `generate`). |
| `ArchitectLuna.Templates` | `QueryEndpoint.cs.sbn` gains a paged-collection branch (binds `page`/`pageSize`, maps `PagedResult<Result>`→`PagedResult<Response>`); render models carry paging fields. |
| `ArchitectLuna.Adapters.MediatR` / `.Wolverine` | Paged `GetAll`: message carries `Page`/`PageSize`, result type `Result<PagedResult<{Op}Result>>`, endpoint binds query-string paging and returns `PagedResult<{Op}Response>`. |
| `ArchitectLuna.Persistence.EfCore` | `AddPersistence` (DbContext + `EnableRetryOnFailure` + interface map + `DatabaseInitializer` hosted service + DB health check); generated `DatabaseInitializer` (migrate-if-migrations-exist-else-EnsureCreated) and `DatabaseHealthCheck` (`CanConnectAsync`); `GetAll` binding returns a `PagedResult` with `Skip`/`Take`/`CountAsync`. New package: `Microsoft.EntityFrameworkCore.Design`. |
| `ArchitectLuna.Persistence.Marten` | `AddPersistence` (`RegisterDocumentType<T>` per entity + `ApplyAllDatabaseChangesOnStartup()` + Marten health check); generated `MartenHealthCheck`; paged `GetAll` (`Skip`/`Take` + `CountAsync`). |
| `ArchitectLuna.Persistence.InMemory` | `AddPersistence` (store, and interface map under Clean Architecture); paged `GetAll` (`Skip`/`Take`/`Count`). |
| `ArchitectLuna.Core/Generation/NullPersistenceGenerator` | no-op `AddPersistence`. |
| `ArchitectLuna.Cli` (`FoundationFiles`) | `AddInfrastructure` calls `services.AddPersistence(configuration)`; `EndpointExtensions` splits `/health` (liveness) from `/health/ready` (readiness, DB-tagged checks). |
| Tests | `Template.Tests`: assert `AddPersistence` content (schema/health/registration) + paged GetAll shape; `Core.Tests`: paged synthesis; `EndToEnd`: unchanged matrix still builds, plus a real Postgres run of EF Core + Marten if a database is reachable. |
| Docs | README/ARCHITECTURE/ROADMAP: schema-at-startup, health readiness, pagination. |

## Design decisions

1. **Persistence registration becomes a `generate`-regenerated `AddPersistence` file**, emitted by
   `GenerateSolutionPersistence` (scaffold-time with zero entities, `generate`-time with all).
   This is the same "always emit, re-render with real content once entities exist" pattern the
   DbContext already uses, and it's what lets Marten register each document type — impossible with
   the old scaffold-time-only `BuildServiceRegistration(context)` that never saw the entities.
   `AddInfrastructure` (foundation-owned) just calls into it.
2. **EF Core schema: migrate-if-migrations-exist-else-`EnsureCreated`.** A generator can't
   pre-build a migration (needs a design-time compile), so out of the box `EnsureCreated` makes the
   app runnable immediately; the `Design` package + docs let a team adopt migrations, at which
   point the initializer applies them instead. Idempotent, safe to run every boot.
3. **Marten schema: `RegisterDocumentType<T>` per entity + `ApplyAllDatabaseChangesOnStartup()`.**
   Registers every generated document up front and creates/updates its table at startup — the
   "extensions to register tables" that were missing.
4. **Readiness vs liveness.** `/health` stays a cheap liveness probe (no checks); `/health/ready`
   runs the DB-tagged checks, so an orchestrator can tell "process up" from "can serve traffic".
   Uses `HealthCheckOptions` from the shared framework — no new package.
5. **Pagination shape.** Paged `GetAll` keeps the `GET /api/{feature}` collection route and binds
   `?page=1&pageSize=20` from the query string (optional, sensible defaults). The message carries
   `Page`/`PageSize`; the handler returns `Result<PagedResult<{Op}Result>>`; the endpoint maps to
   `PagedResult<{Op}Response>`. Providers do `Skip((page-1)*pageSize).Take(pageSize)` + a total
   `Count`. `PageSize` is clamped in the handler body to a sane max to avoid unbounded reads.
6. **Connection resilience.** EF Core registers `EnableRetryOnFailure()` — standard production
   default for transient DB faults.

## Invariant check

- [x] Adapter parity — paged GetAll rendered from the shared `QueryEndpoint` template; both adapters get identical HTTP shape.
- [x] Core stays framework-free — `AddPersistence`/health/initializer are provider-generated *content*, not Core references.
- [x] Adapters/providers do no file I/O — all new output is `GeneratedFile` records.
- [x] Protected regions survive — handler bodies keep their region markers.
- [x] `HandlerBinding` single dependency — paged GetAll still injects exactly one (DbContext/session/store).
- [x] Template gotchas — no new `.sbn` files (only edits); handlers stay async; Wolverine keeps explicit `CancellationToken`.

## Steps

1. Core: `QueryModel.IsPaged`, `CrudSynthesizer` paged GetAll, `IPersistenceGenerator` interface swap (`AddPersistence` file, drop `BuildServiceRegistration`). Build.
2. Providers: `AddPersistence` + schema/health/initializer files + paged GetAll bindings (Ef/Marten/InMemory/None). Build.
3. FoundationFiles: `AddInfrastructure` → `AddPersistence`; `/health` + `/health/ready`. Build.
4. Templates + adapters: paged query endpoint + render models. Build + scaffold/generate/build one EF + one Marten + one in-memory solution.
5. Real run: stand up Postgres, run the generated EF Core and Marten APIs, curl create + paged GetAll + verify tables exist and `/health/ready` flips with the DB.
6. Tests: Template.Tests (AddPersistence content, paged shape), Core.Tests (paged synthesis), fix any E2E assertions. Docs. Verify. Push.

## Test plan

- Core.Tests: `CrudSynthesizer` marks GetAll paged with Page/PageSize; route still collection.
- Template.Tests: `AddPersistence` contains schema application (EnsureCreated/Migrate for EF,
  ApplyAllDatabaseChangesOnStartup + RegisterDocumentType for Marten), a DB health check tagged
  `ready`, and EF connection resilience; paged GetAll endpoint binds page/pageSize and returns
  `PagedResult<Response>`; both adapters identical.
- EndToEnd: existing scaffold/generate/build matrix stays green; add a Postgres-backed run of
  `efcore-postgres` and `marten` (create → paged GetAll → 200 with paging metadata) guarded to
  skip when no database is reachable, so CI without a DB service still passes.
- Manual: real Postgres run of both, confirming tables are auto-created and pagination works.

## Out of scope

- CORS, rate limiting, OpenTelemetry/tracing, API versioning, authN/Z scaffolding — genuinely
  production concerns but not named here; tracked as future roadmap items.
- SQL Server real-run verification (Postgres covers EF Core + Marten; SQL Server shares the EF
  Core path, verified by build only unless a SQL Server instance is reachable).
- Cursor/keyset pagination (offset paging is the V1 shape).

## Outcome (fill in at delivery)

- What shipped, deviations, follow-ups.
