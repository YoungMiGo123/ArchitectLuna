# Plan NNN: <Feature name>

- **Status:** Draft | In progress | Done
- **Complexity:** M | L (see rubric in `docs/workflow/feature-workflow.md`)
- **Author:** <agent/model or human name>
- **Date started:** YYYY-MM-DD
- **Checklist used:** <link to `docs/workflow/checklists/...` if one applies, else "none">

## Summary

One paragraph: what will exist after this change that doesn't exist now, and why it's worth
building. Written so someone who hasn't read the conversation understands the goal.

## Affected components

List every project/file group this change touches. Derive from the component map in
`docs/ARCHITECTURE.md`; an empty row means "consciously unaffected", not "didn't check".

| Component | Change |
|---|---|
| `ArchitectLuna.Core` | |
| `ArchitectLuna.Templates` (`.sbn` files) | |
| `ArchitectLuna.Adapters.MediatR` | |
| `ArchitectLuna.Adapters.Wolverine` | |
| `ArchitectLuna.Persistence.EfCore` / `.Marten` | |
| `ArchitectLuna.Cli` (commands, registries, scaffolder) | |
| `ArchitectLuna.Ui` | |
| Tests (Core.Tests / EndToEnd.Tests) | |
| Docs / CI (`.github/workflows/ci.yml`) | |

## Design decisions

The choices that shape the implementation, each with a one-line rationale and the alternative
rejected. If a seam interface (`IFrameworkAdapter`, `IPersistenceGenerator`) changes shape,
justify it here — those changes ripple to every implementation.

## Invariant check

Confirm against the six invariants in `docs/workflow/feature-workflow.md` §Stage 4. Note any
invariant this change intentionally relaxes and why.

- [ ] Adapter parity preserved (or exclusion justified)
- [ ] Core stays framework-free
- [ ] Adapters do no file I/O
- [ ] Protected regions survive regeneration
- [ ] `HandlerBinding` single-dependency cap respected
- [ ] Template gotchas handled (`WithCulture="false"`, async handlers, Wolverine `CancellationToken`)

## Steps

Ordered, independently verifiable. Each step: files touched + how you know it worked.

1. **<Step name>** — files: `...` — verify: `...`
2. ...

## Test plan

- Unit tests to add/modify in `ArchitectLuna.Core.Tests`: ...
- E2E coverage: which adapter × persistence combinations does this affect, and which test in
  `ArchitectLuna.EndToEnd.Tests` proves each? New matrix axis needed in CI?
- Manual verification (if any): ...

## Out of scope

What this change deliberately does not do, so reviewers don't expect it and future work can
pick it up.

## Outcome (fill in at delivery)

- What shipped, with commit hashes.
- Deviations from the plan above and why.
- Follow-ups discovered (add real ones to `docs/ROADMAP.md`).
