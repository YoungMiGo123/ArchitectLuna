# Plan 002: Pagination for generated GetAll queries

- **Status:** Done — verified during the merge with plan 003 (see Outcome addendum below)
- **Complexity:** L (touches `ArchitectLuna.Core/Model/*.cs`, an automatic-L trigger)
- **Author:** Claude
- **Date started:** 2026-07-09
- **Checklist used:** none (not a new adapter/provider/CLI command/template)

## Summary

Every CRUD-synthesized `GetAll{Entity}` query currently loads the entire table with no limit,
regardless of adapter, persistence provider, or solution layout — `FoundationFiles` already
defines a `PagedResult<T>` type but nothing produces or consumes it (confirmed dead code). After
this change, `GetAll` queries accept `page`/`pageSize` query-string parameters, persistence
generators page with `Skip`/`Take` (or the provider-appropriate equivalent) instead of scanning
the full table, and the HTTP response carries `items` + paging metadata
(`page`/`pageSize`/`totalCount`/`totalPages`/`hasNextPage`/`hasPreviousPage`). This is
adapter/persistence/layout-independent — it holds for both `mediatr`/`wolverine`, all three
persistence providers, and both `vertical-slice`/`clean-architecture` layouts, matching how every
other piece of the production foundation (Result pattern, Serilog, health checks, middleware) is
already shared identically across layouts via `FoundationFiles.BuildAll`.

Explicitly out of scope: hand-authored `add query --collection` queries (not CRUD-synthesized)
keep today's unbounded-list behavior — only `CrudSynthesizer`'s `GetAll` gets paged, since that's
the one the user's complaint and the dead `PagedResult<T>` type were about.

## Affected components

| Component | Change |
|---|---|
| `ArchitectLuna.Core` | `QueryModel` gains `IsPaged`; `CrudSynthesizer` sets it on the synthesized GetAll query. No change to `IFrameworkAdapter`/`IPersistenceGenerator` interface shape. |
| `ArchitectLuna.Templates` (`.sbn` files) | None — the existing `[AsParameters]` branch in `QueryEndpoint.cs.sbn` already handles a multi-field query message; page/pageSize ride the same mechanism as any other query field. |
| `ArchitectLuna.Adapters.MediatR` | `GenerateQuery`: append Page/PageSize fields to the message when `IsPaged`, wrap result type as `Result<PagedResult<T>>`, paged success expression. |
| `ArchitectLuna.Adapters.Wolverine` | Same three changes, mirrored for adapter parity. |
| `ArchitectLuna.Persistence.EfCore` / `.Marten` / `.InMemory` | `BindQueryHandler`: paged `GetAll` body — count + `Skip`/`Take` ordered by `Id`, clamping page/pageSize defaults (page<=0 → 1, pageSize<=0 → 20) since query-string binding leaves missing ints at 0. |
| `ArchitectLuna.Cli` (commands, registries, scaffolder) | None. |
| `ArchitectLuna.Ui` | None (read/edit model only, doesn't render query bodies). |
| Tests (Core.Tests / Template.Tests / EndToEnd.Tests) | `CrudSynthesizerTests`: GetAll has `IsPaged = true`. New EndToEnd coverage: a paged GetAll actually pages (create N > pageSize entities, assert `items.Count == pageSize` and `totalCount == N`) for at least one adapter × persistence combination; ideally the full matrix if time allows. |
| Docs / CI | `docs/ROADMAP.md`: move this from implicit gap to a Done entry. |

## Design decisions

1. **`IsPaged` as a new `QueryModel` flag, not overloading `IsCollection`.** Keeps hand-authored
   collection queries (`add query --collection`, no entity link) on today's unbounded behavior;
   only entity-backed `CrudSynthesizer` output opts in. Alternative rejected: making all
   `IsCollection` queries paged — would silently change behavior for bespoke queries with no
   natural page/pageSize semantics (e.g. a query with existing filter params).

2. **Page/PageSize never enter `QueryModel.Params`.** `RouteInference.QueryRoute` and the
   adapters' `isZeroParam`/`isSingleRouteParam` route-shape logic key off `Params.Count`. Keeping
   `Params` at zero (business filter params only) means the route stays the plain collection route
   (`GET /api/{feature}`) with zero changes to `RouteInference`. Page/PageSize are synthesized
   directly onto the message's field list in the adapters, alongside `Params`, only for rendering
   the message record and computing route-shape flags (which must also ignore them — see step 2
   below). Alternative rejected: adding Page/PageSize to `Params` — would misroute GetAll to
   `/api/{feature}/get-all-{entity}` (the "≥2 params, no bespoke route" fallback) and break the
   existing REST shape.

3. **No new Contracts DTO/template for the paged envelope.** `PagedResult<T>` (Application) is
   projected straight into an anonymous object in the endpoint's success expression
   (`Results.Ok(new { items = ..., result.Value.Page, ... })`) rather than generating a typed
   `PagedResponse<T>` via `Record.cs.sbn`/`Mappings.cs.sbn`. Keeps the change to adapter C# only —
   no template or Contracts-project changes, smaller blast radius, and ASP.NET happily serializes
   anonymous types. Accepted tradeoff: the OpenAPI/Swagger schema for a paged response won't be as
   precise as a named record; flagged as a documented follow-up, not silently dropped.

4. **Missing page/pageSize in the query string defaults via handler-body clamping, not C# default
   parameter values on the record.** `[AsParameters]` binding of a positional record leaves an
   unsupplied `int` at its CLR default (`0`); rather than teach `Message.cs.sbn` about per-field
   default literals (a template change touching every message, paged or not), each
   `IPersistenceGenerator`'s paged `GetAll` body clamps `page <= 0 → 1` and `pageSize <= 0 → 20`
   before querying. Keeps the default-handling logic co-located with the one place per provider
   that already renders the collection body, instead of spreading it across three projects'
   worth of template edits.

## Invariant check

- [x] Adapter parity preserved — MediatR and Wolverine both get the identical three changes
      (message fields, result type, success expression); dispatch-call plumbing already handles
      an N-field query via the existing `else` branch in both adapters.
- [x] Core stays framework-free — `QueryModel.IsPaged` is a plain bool; no EF Core/Marten/MediatR/
      Wolverine/Scriban reference enters `Core`.
- [x] Adapters do no file I/O — unchanged; still returning `GeneratedFile` records.
- [x] Protected regions survive — handler bodies still render inside
      `// <architect:region name="handler-body">`; only the body *content* for paged `GetAll`
      changes, the merge mechanism is untouched.
- [x] `HandlerBinding` single-dependency cap respected — paged bodies still use the one existing
      injected dependency (`dbContext`/`session`/`store`); no second dependency introduced.
- [x] Template gotchas — no new `.sbn` files, so `WithCulture="false"` doesn't apply; handlers
      remain `async`; Wolverine's `Handle` already declares `CancellationToken cancellationToken`
      unconditionally, reused as-is.

## Steps

1. **`QueryModel.IsPaged`** — files: `ArchitectLuna.Core/Model/QueryModel.cs` — verify: builds;
   default `false` so no other query is affected.
2. **`CrudSynthesizer` sets `IsPaged = true`** on the synthesized `GetAll` query — files:
   `ArchitectLuna.Core/Model/CrudSynthesizer.cs` — verify:
   `dotnet test tests/ArchitectLuna.Core.Tests` (existing + new `CrudSynthesizerTests` assertion).
3. **MediatR adapter paging** — files: `ArchitectLuna.Adapters.MediatR/MediatRAdapter.cs`
   (`GenerateQuery`) — verify: `dotnet test tests/ArchitectLuna.Template.Tests` snapshot for a
   paged query's Message/Handler/Endpoint shape.
4. **Wolverine adapter paging** — files: `ArchitectLuna.Adapters.Wolverine/WolverineAdapter.cs`
   (`GenerateQuery`) — mirror step 3 exactly — verify: same Template.Tests tier, Wolverine
   snapshot.
5. **EF Core paged `GetAll` body** — files:
   `ArchitectLuna.Persistence.EfCore/EfCorePersistenceGenerator.cs` (`BindQueryHandler`,
   `RenderGetAllBody` → new `RenderPagedGetAllBody`) — verify: `dotnet build`, then EndToEnd.
6. **Marten paged `GetAll` body** — files:
   `ArchitectLuna.Persistence.Marten/MartenPersistenceGenerator.cs` — mirror step 5.
7. **InMemory paged `GetAll` body** — files:
   `ArchitectLuna.Persistence.InMemory/InMemoryPersistenceGenerator.cs` — mirror step 5 (no
   `async`/DB round trip needed; still returns `PagedResult<T>` with the same clamping).
8. **EndToEnd coverage** — files: `ArchitectLuna.EndToEnd.Tests/...` — new test(s) creating more
   entities than one page and asserting `items.Count`, `totalCount`, `totalPages` on the JSON
   response — verify: `dotnet test ArchitectLuna.sln`.
9. **Docs** — `docs/ROADMAP.md` Done entry describing the change.

## Test plan

- `ArchitectLuna.Core.Tests`: `CrudSynthesizerTests` asserts `GetAll` query has `IsPaged == true`
  and `Params` stays empty (route-shape invariant from design decision 2).
- `ArchitectLuna.Template.Tests`: a paged-query snapshot per adapter proving the message has
  Page/PageSize fields, result type is `Result<PagedResult<T>>`, and the endpoint's success
  expression builds the `items`/paging envelope.
- `ArchitectLuna.EndToEnd.Tests`: at minimum one adapter × persistence combination (extend to the
  full matrix if the build environment allows) — scaffold, generate, `dotnet build`, run the API,
  seed > one page of entities via the Create endpoint, `GET /api/{feature}?page=1&pageSize=N` and
  assert the paging envelope is correct; a second request for `page=2` proves `Skip`/`Take` are
  wired, not just `Take`.
- Manual verification: none possible in this environment — **no `dotnet` SDK is installed in this
  sandbox**, so `dotnet build`/`dotnet test` cannot be run here. This is called out explicitly in
  the Outcome section; the user (or CI) must run the verification matrix above before merging.

## Out of scope

- Hand-authored `add query --collection` queries (no `EntityName`) — unaffected, still unbounded.
- A typed Contracts `PagedResponse<T>` DTO / Swagger schema precision (see design decision 3) —
  follow-up, not part of this change.
- Cursor-based pagination — offset (`Skip`/`Take`) only, matching the existing simplicity
  philosophy of generated handler bodies.
- EF Core migrations / index tuning for the `Id` ordering used to make `Skip`/`Take`
  deterministic — assumed acceptable for the generated MVP scaffold, as with every other
  generated persistence code today.

## Outcome (fill in at delivery)

Steps 1-7 and 9 shipped as designed: `QueryModel.IsPaged`, `CrudSynthesizer` setting it on the
synthesized `GetAll` query, both adapters (`MediatRAdapter`/`WolverineAdapter`) appending
Page/PageSize to the message and wrapping the result as `Result<PagedResult<T>>`, and all three
persistence generators (`EfCorePersistenceGenerator`, `MartenPersistenceGenerator`,
`InMemoryPersistenceGenerator`) rendering a Skip/Take-based paged `GetAll` body ordered by `Id`,
with page/pageSize clamped to sane defaults (1 / 20) when the query string omits them. New
`PaginationSnapshotTests` (Template.Tests) cover message shape, result type, route stability, all
three providers' handler bodies, and the endpoint's response envelope; `CrudSynthesizerTests` and
the existing `CrudGenerationSnapshotTests` endpoint-status-code test were updated for the new
success expression.

**Deviation from plan — step 8 (EndToEnd HTTP coverage) not done.** The plan's test-plan called
for an EndToEnd test that scaffolds, builds, runs the generated API, and curls page 1/page 2 to
prove `Skip`/`Take` are wired correctly end to end. **This sandbox has no `dotnet` SDK installed**
(confirmed: `dotnet build`/`dotnet test` both fail with "command not found"), so nothing in this
change — including the pre-existing suite — could be built, run, or verified here. Writing new
EndToEnd tests un-compiled and un-run would be guessing at API/text-matching details with real
risk of being subtly wrong (exact Marten `CountAsync` overload resolution, EF Core `LongCountAsync`
signature, etc.) and no way to self-correct. Authoring the Template.Tests-tier snapshot assertions
was judged safe (string-matching bodies I could construct precisely from the existing code I read),
but the full build+run+curl EndToEnd matrix is a genuine gap.

**Follow-ups / what the user (or CI) must still do before merging:**
1. Run `dotnet build ArchitectLuna.sln` — the three persistence generators' Marten `CountAsync`
   call in particular is unverified (assumed to resolve via the same `using Marten;` that already
   resolves `ToListAsync` on the same `IMartenQueryable<T>`, but not compiler-checked here).
2. Run `dotnet test tests/ArchitectLuna.Core.Tests` and `dotnet test tests/ArchitectLuna.Template.Tests`
   (fast tiers, should be quick to iterate on if anything's off).
3. Run `dotnet test ArchitectLuna.sln` for the full E2E matrix, and ideally add the HTTP-level
   page-1/page-2 test described in step 8 above — flagged in `docs/ROADMAP.md` as unfinished.
4. A typed Contracts `PagedResponse<T>` DTO (design decision 3's accepted tradeoff) remains a
   follow-up if precise OpenAPI/Swagger schemas for paged responses matter.

## Addendum — verification (merged alongside plan 003)

This plan's build/test verification (items 1-3 above) happened as part of reconciling with
[plan 003](003-runnable-persistence-and-schema-init.md), which independently implemented the same
`IsPaged` feature on a separate branch before this plan merged to `master`. The two were combined:
this plan's design (Params stays empty; Page/PageSize synthesized directly in the adapters; the
endpoint success expression projects an anonymous `{ items, page, pageSize, totalCount,
totalPages, hasNextPage, hasPreviousPage }`) was kept as the paging *mechanism* since it avoids
touching `RouteInference` — plan 003 layered its independently-built persistence-runnability work
(startup schema creation, DB health checks) on top and additionally **capped `PageSize` at 100**
(`Math.Min(message.PageSize, 100)` instead of an unbounded default-only clamp) as a production
safety measure; `PaginationSnapshotTests`' `HandlerBody_SkipsAndTakes_WithClampedDefaults` was
updated to match. `dotnet build ArchitectLuna.sln` and the fast test tiers were run as part of that
merge; `PaginationSnapshotTests` all pass. The full E2E matrix and the HTTP-level page-1/page-2
test (item 3 above) were verified against a live Postgres instance, not just `dotnet build` — see
plan 003's Outcome for the run details.
