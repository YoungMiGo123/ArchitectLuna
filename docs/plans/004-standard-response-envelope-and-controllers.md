# Plan 004: Standard API response envelope + Controller output

- **Status:** In progress
- **Complexity:** L (spans the adapter × persistence matrix, changes generated-output shape,
  adds a new CLI-level option)
- **Author:** Claude
- **Date started:** 2026-07-09
- **Checklist used:** hybrid of `docs/workflow/checklists/template-change.md` and
  `new-cli-command.md` (no single checklist matches "response envelope + new API style")

## Summary

Every generated API currently returns ad-hoc shapes: `Results.Ok(...)`/`Results.Created(...)`
with a bare response DTO on success, and `Results.Problem(...)`/`Results.ValidationProblem(...)`
(RFC7807 `problem+json`) on failure. Per `docs/requirements/004-standards-return-types.md`, every
non-empty response must instead be wrapped in a standard `ApiResponse<T>` envelope
(`{ success, payload, error }`), with `ApiError` carrying `code`/`message`/`type`/
`validationErrors`. Result→response mapping must be centralized (`ResultExtensions`:
`ToOkResponse`/`ToCreatedResponse`/`ToNoContentResponse`/`ToErrorResponse`) instead of endpoints
hand-rolling the mapping. The envelope must be identical across MediatR/Wolverine and across
EF Core/Marten/in-memory/none persistence, and — new capability — across Minimal API and
Controller-based output, selectable via a new `--api-style` flag on `new api` (mirroring
`--adapter`/`--persistence`/`--architecture`).

## Affected components

| Component | Change |
|---|---|
| `ArchitectLuna.Core` | New `ApiStyle` enum (`Model/ApiStyle.cs`); `IFrameworkAdapter.GenerateCommand/GenerateQuery` gain no new params — style is resolved to a template-family choice inside each adapter via a new `ApiStyle` argument threaded from `GenerateCommand`. |
| `ArchitectLuna.Templates` (`.sbn` files) | `Shared/CommandEndpoint.cs.sbn`/`QueryEndpoint.cs.sbn` updated to call centralized `ResultExtensions`; new `Shared/CommandController.cs.sbn`/`QueryController.cs.sbn` for controller output. |
| `ArchitectLuna.Adapters.MediatR` / `.Wolverine` | Both build `successExpression` as a `ResultExtensions` call instead of a raw `Results.*` call; both pick the endpoint vs controller template based on `ApiStyle`; identical branching in both (parity). |
| `ArchitectLuna.Persistence.EfCore` / `.Marten` / `.InMemory` / none | Unaffected — verified, not just assumed (orthogonal seam). |
| `ArchitectLuna.Cli` (commands, registries, scaffolder) | New `--api-style` flag on `NewApiCommandSettings` (`minimal-api` default, `controllers`); `ArchitectModel.ApiStyle`; `GenerateCommand` reads it and passes to adapters; `FoundationFiles` emits `ApiResponse<T>`/`ApiError`/`ResultExtensions`/`ResultActionExtensions`/`PagedResponse<T>`, and conditionally registers `AddControllers()`/`MapControllers()` instead of the `IEndpointDefinition` scan when `ApiStyle.Controllers`. |
| `ArchitectLuna.Ui` | Out of scope — no UI surface for this flag yet (recorded as a gap below). |
| Tests (`Template.Tests` / `EndToEnd.Tests`) | Update existing snapshot assertions broken by the new envelope; add new assertions per requirements §17; add an E2E build-matrix axis for `--api-style controllers`. |
| Docs / CI | README "What generate produces" table; `docs/ROADMAP.md`; no CI changes needed (existing smoke matrix loops adapter × persistence — add api-style as a third axis only if trivial, otherwise note the gap). |

## Design decisions

1. **`ApiResponse<T>`/`ApiError` live in `Api/Responses/`, `ResultExtensions` in `Api/Results/`**,
   per requirements §16 — a new location, not reusing `Common/`. `Common/ResultHttpExtensions.cs`
   (`ToProblem()`) is deleted; nothing outside generated endpoint templates called it.
2. **Success-branch centralization**: instead of endpoints doing
   `result.IsSuccess ? successExpression : result.ToProblem()`, `successExpression` itself becomes
   the full `result.ToXResponse(...)` call (it internally short-circuits to
   `ToErrorResponse()` on failure) — matches requirements §8/§10 exactly and lets the endpoint
   body shrink to `return {{ success_expression }};`.
3. **Controllers are a second, parallel endpoint-rendering family, not a new `IFrameworkAdapter`.**
   The dispatch/message/handler side (MediatR vs Wolverine) is completely unaffected; only which
   `Shared/*.sbn` template renders the HTTP layer changes. This keeps `new-framework-adapter.md`'s
   heavier process out of scope — it doesn't apply here.
4. **Controller actions use `IActionResult`, not `IResult`.** A parallel `ResultActionExtensions`
   (`ToOkActionResponse`/`ToCreatedActionResponse`/`ToNoContentActionResponse`/
   `ToErrorActionResponse`) is generated alongside `ResultExtensions`, sharing the same
   `ApiResponse<T>`/`ApiError`/status-code mapping logic (factored into one private helper) so the
   two families can never drift.
5. **One controller-with-one-action per operation**, matching today's one-endpoint-class-per-
   operation granularity — not one controller-per-resource with multiple actions (the requirements
   doc's illustrative example). This avoids inventing cross-action route lookups
   (`nameof(GetById)`) that don't fit the current per-operation file generation model;
   `Created(location, value)` with a literal location string (identical to the Minimal API
   `Results.Created` call) gives the same 201 + Location header semantics without it.
6. **Paged responses get a typed `PagedResponse<T>` Contracts DTO**, replacing today's anonymous
   object (a documented gap in `docs/plans/002-crud-getall-pagination.md`), because
   `.Produces<ApiResponse<T>>()` OpenAPI metadata can't describe an anonymous type usefully.
7. **`ExceptionHandlingMiddleware`'s catch-all responses also switch to the envelope** (were
   `problem+json`), since requirements §2/§18 say every non-empty response — not just Result
   failures — must be wrapped.
8. **`--api-style` mirrors `--architecture`/`--adapter` exactly**: CLI flag → known-values
   validation → enum → stored on `ArchitectModel` → read back in `GenerateCommand` → passed to
   `FoundationFiles.BuildAll` and both adapters.

## Invariant check

- [x] Adapter parity preserved — both adapters build `successExpression` via the same
      `ResultExtensions`/`ResultActionExtensions` call shapes and pick the same template per style.
- [x] Core stays framework-free — `ApiStyle` enum only; no ASP.NET/MediatR/Wolverine types in Core.
- [x] Adapters do no file I/O — unchanged, still return `GeneratedFile` records.
- [x] Protected regions survive regeneration — no protected-region markers touched.
- [x] `HandlerBinding` single-dependency cap respected — untouched, this change is entirely at the
      HTTP boundary above the handler.
- [x] Template gotchas handled — new `.sbn` files added with `WithCulture="false"` embedded
      resources; handlers remain unconditionally async (unchanged); Wolverine handlers still
      declare `CancellationToken` explicitly (unchanged).

## Steps

1. **Envelope + centralized extensions in `FoundationFiles`** — files:
   `src/ArchitectLuna.Cli/Scaffolding/FoundationFiles.cs`. Verify: `dotnet build`.
2. **Wire Minimal API templates + adapters to the new extensions** — files:
   `Templates/Shared/CommandEndpoint.cs.sbn`, `QueryEndpoint.cs.sbn`,
   `ArchitectLuna.Adapters.MediatR/MediatRAdapter.cs`, `ArchitectLuna.Adapters.Wolverine/WolverineAdapter.cs`.
   Verify: `dotnet test tests/ArchitectLuna.Template.Tests` (after step 5 updates its assertions).
3. **OpenAPI metadata** — `.Produces<ApiResponse<...>>()` chains on generated endpoints (both
   Minimal API and Controllers). Verify: snapshot assertion + a real `dotnet build` + Swagger JSON
   inspection on one scaffolded sample.
4. **`--api-style` flag + Controllers template family** — files: `Core/Model/ApiStyle.cs`,
   `ArchitectModel.cs`, `NewApiCommand.cs`, `GenerateCommand.cs`, new
   `Templates/Shared/CommandController.cs.sbn`/`QueryController.cs.sbn`, adapter branching,
   `FoundationFiles` controllers-mode DI wiring (`AddControllers()`/`MapControllers()`).
   Verify: scaffold + generate + `dotnet build` a real `--api-style controllers` sample for both
   adapters.
5. **Tests** — update broken snapshot assertions, add new ones from requirements §17, add E2E
   coverage for controllers. Verify: full `dotnet test ArchitectLuna.sln`.
6. **Docs** — README generate-output table, `docs/ROADMAP.md` Done entry, this plan's Outcome
   section.

## Test plan

- `ArchitectLuna.Template.Tests`: update `Endpoints_MapResultsToConsistentStatusCodes`-style
  assertions (they currently assert `"Results.Created("`, `"result.ToProblem()"`) to assert
  `"ToCreatedResponse("`/`"ToOkResponse("`/`"ToNoContentResponse("`/`"ApiResponse<"` instead, for
  both Vertical Slice and Clean Architecture, both adapters. Add controllers-mode equivalents.
- `ArchitectLuna.EndToEnd.Tests`: add `--api-style controllers` rows to
  `GeneratedSolutionBuildTests` for both adapters × representative persistence providers (in-memory
  is enough — persistence is orthogonal, already proven).
- Manual: scaffold one sample with each style, `dotnet run`, curl a create/get/delete, confirm the
  JSON body shape matches requirements §3/§4/§5 exactly.

## Out of scope

- `ArchitectLuna.Ui` does not get an API-style picker in this change (gap noted in ROADMAP).
- Optional `ToAcceptedResponse`/`ToPagedResponse` extensions beyond the required four are added
  only if trivial; not a hard requirement.
- No change to the `external_dependency`/`timeout`/`rate_limited`/`concurrency`/`invalid_state`
  optional future error types — `ErrorType` enum keeps today's six values.

## Outcome (fill in at delivery)

- TBD.
