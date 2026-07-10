# Feature plans

Plan documents for **L-complexity** changes, written before implementation as required by
`docs/workflow/feature-workflow.md`. Create one by copying
`docs/workflow/templates/feature-plan.md` to `NNN-short-slug.md` (next sequential number,
zero-padded: `001-adapter-switch.md`, `002-clean-architecture-layout.md`, …).

Plans are living documents during implementation and a permanent record afterward: each ends with
an **Outcome** section describing what actually shipped and how it deviated from the plan. Do not
delete completed plans — they are the design history of the project.

| # | Plan | Status |
|---|---|---|
| 001 | [Production foundation + testing layer](001-production-foundation-and-testing-layer.md) | Done |
| 002 | [Pagination for generated GetAll queries](002-crud-getall-pagination.md) | Implemented, verification pending |
| 003 | [Generation quality, entity sync, and database readiness](003-generation-quality-and-db-readiness.md) | Implemented, full E2E matrix verification pending (see Outcome) |
