# Roadmap

Phases are sequential; each is expected to land as its own set of PRs rather
than one large drop.

## Phase 0 — Planning (this phase)

- `README.md`, `ARCHITECTURE.md`, `PATTERNS.md`, `ROADMAP.md`.
- No code. Establishes the spec shape, determinism guarantees, and pattern
  boundaries that later phases build against.

## Phase 1 — Repo scaffold

- .NET solution structure matching the components table in
  `ARCHITECTURE.md` (`InveroArchitect.Core`, `.Generation`, `.Cli`, one
  `.Templates.*` project per pattern, matching test projects).
- Build, formatting (`dotnet format`), and CI wired up (build + test on PR).
- No generation logic yet — projects exist, compile, and are empty.

## Phase 2 — Core pipeline + Entities pack (MVP)

- Spec schema finalized, parser, IR model in `Core`.
- Rendering pipeline, canonical formatter pass, deterministic file writer in
  `Generation`.
- `InveroArchitect.Templates.Entities` pack: entities, value objects,
  invariants, domain events.
- `invero generate <spec>` works end to end for the Entities pack.
- `invero doctor` (double-run idempotency check) exists and is part of CI
  for the pack.

## Phase 3 — Vertical Slice pack

- `InveroArchitect.Templates.VerticalSlice`, built on the Phase 2 Entities
  output.
- `useCases` spec section fully supported (command/query, handler, endpoint,
  validator per slice).

## Phase 4 — Clean Architecture pack

- `InveroArchitect.Templates.CleanArchitecture`, full multi-project solution
  generation including `.sln`/project-reference wiring.
- Shares entity/use-case rendering with Phases 2–3 rather than
  reimplementing it.

## Phase 5 — Generic pack authoring SDK

- Documented, stable `IR node → files` contract extracted for third-party
  packs (see `PATTERNS.md` § Generic multi-target templates).
- A minimal example pack outside the three built-ins, to prove the contract
  holds for an architecture style not designed by this project.

## Phase 6 — Regeneration & merge tooling

- Protected-region / partial-class extension points enforced by the writer
  (`ARCHITECTURE.md` § generated vs. hand-authored separation).
- `invero diff` to preview regeneration changes against an existing tree
  before writing.
- Spec migration support (evolving a spec and regenerating without losing
  hand-authored additions).
