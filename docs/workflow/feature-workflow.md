# Feature Development Workflow

A structured, repeatable process for making changes to ArchitectLuna. It is written for **any
agent or developer** — human, Claude, or another model — so every change follows the same shape:
classify the work, plan proportionally to its complexity, implement against the plan, verify
against the matrix, and deliver with a traceable record.

The workflow has five stages. Simple changes flow through in minutes; complex features get an
automatic written plan before any code is touched.

```
1. INTAKE ──▶ 2. CLASSIFY ──▶ 3. PLAN ──▶ 4. IMPLEMENT ──▶ 5. VERIFY & DELIVER
                 S / M / L      (auto for M/L)
```

---

## Stage 1 — Intake

Restate the request in one or two sentences: **what should exist after this change that doesn't
exist now?** Resolve ambiguity here, not mid-implementation. Read the docs that bound the design
space before touching code:

- `README.md` — what the tool does, CLI surface, what `generate` produces
- `docs/ARCHITECTURE.md` — the pipeline, the two seams (`IFrameworkAdapter`,
  `IPersistenceGenerator`), and the known gotchas
- `docs/PATTERNS.md` — code conventions
- `docs/ROADMAP.md` — check whether the request is already a planned milestone; if so, follow its
  described shape

Then locate the affected code with the **component map** in `docs/ARCHITECTURE.md`.

## Stage 2 — Classify complexity

Score the change against this rubric. When in doubt, round **up** — an unnecessary plan costs
minutes; an unplanned complex change costs hours of rework.

| Size | Criteria (any one is enough) | Planning required |
|---|---|---|
| **S — Simple** | Single project; no changes to templates, public contracts, or the model schema; ≤ ~3 files; existing tests cover the area | None. Implement directly with a short task list. |
| **M — Moderate** | Touches `.sbn` templates or generated-output shape; crosses 2–3 projects; adds a test category; changes CLI options on an existing command | **Inline plan**: write the plan sections (affected components, steps, test plan) into your working notes / PR description before coding. |
| **L — Complex** | New adapter, persistence provider, or CLI command; any change to `IFrameworkAdapter`, `IPersistenceGenerator`, `HandlerBinding`, or the `model.yaml` schema; anything that must hold across the adapter × persistence matrix; multi-session work | **Full plan document** in `docs/plans/` from the template, written and reviewed *before* implementation starts. |

**Automatic L triggers** (no judgment needed — these are always L):

- A new implementation of either seam interface
- A change to a file in `ArchitectLuna.Core/Generation/`
- A change to the YAML model shape (`ArchitectLuna.Core/Model/*.cs`)
- Anything the CI smoke matrix (`.github/workflows/ci.yml`) would need a new axis for

## Stage 3 — Plan (automatic for M and L)

For **L** changes, copy `docs/workflow/templates/feature-plan.md` to
`docs/plans/NNN-short-slug.md` (NNN = next number, zero-padded) and fill in every section. For
**M** changes, produce the same sections inline (notes or PR body) — the doc file is optional.

A plan is complete when:

1. **Every affected component is named** — use the component map; a plan that says "update the
   adapters" without listing both `MediatRAdapter` and `WolverineAdapter` is not done.
2. **Steps are ordered and independently verifiable** — each step names the files it touches and
   how you'll know it worked (a test, a build, a generated-output diff).
3. **The test plan covers the matrix** — if generated output changes, say which
   adapter × persistence combinations are affected and which E2E test proves each.
4. **The invariants below have been checked** against the design.
5. **Out-of-scope is explicit** — what this change deliberately does *not* do.

If a recurring change type applies, follow its checklist instead of inventing steps:

- New messaging adapter → `docs/workflow/checklists/new-framework-adapter.md`
- New persistence provider → `docs/workflow/checklists/new-persistence-provider.md`
- New CLI command / option → `docs/workflow/checklists/new-cli-command.md`
- Template (`.sbn`) change → `docs/workflow/checklists/template-change.md`

## Stage 4 — Implement

Work through the plan **in step order**, keeping a live task list (one item per plan step). After
each step, run the fast checks (below) before moving on. If reality diverges from the plan —
a step turns out wrong, a new file needs touching — **update the plan document first**, then
continue. The plan must describe what actually happened, not what was hoped.

### Repo invariants — every change must preserve these

1. **Adapter parity.** Both adapters render endpoints/validators from `Templates/Shared`, share
   `RouteInference`, and produce the identical HTTP surface from the same model. A behavior added
   to one adapter must be added to (or consciously excluded from, in the plan) the other.
2. **Core stays framework-free.** `ArchitectLuna.Core` must not reference MediatR, Wolverine,
   EF Core, Marten, or Scriban. New knowledge of a concrete framework goes in an adapter/provider
   project; the *name* of every adapter/provider is known only to `AdapterRegistry` /
   `PersistenceRegistry` in `ArchitectLuna.Cli`.
3. **Adapters do no file I/O.** An `IFrameworkAdapter` returns `GeneratedFile` records;
   `FileWriter` owns writing and the protected-region merge.
4. **Protected regions survive.** Any change to generated-file shape must keep
   `// <architect:region name="handler-body">` blocks intact through regeneration
   (`ProtectedRegionMergerTests` + `ProtectedRegionRegenerationTests` must stay green).
5. **`HandlerBinding` injects at most one dependency** — see the doc comment in
   `Core/Generation/HandlerBinding.cs` before trying to add a second.
6. **Known template gotchas** (all documented in `docs/ARCHITECTURE.md`): new `.sbn` embedded
   resources need `WithCulture="false"`; `Handle` methods are unconditionally `async`; Wolverine
   handlers must declare `CancellationToken cancellationToken` explicitly.

## Stage 5 — Verify & Deliver

Run checks proportional to what was touched:

| What changed | Required verification |
|---|---|
| Anything | `dotnet build ArchitectLuna.sln` |
| Core / naming / model / merge logic | `dotnet test tests/ArchitectLuna.Core.Tests` |
| Generated-output shape (templates, adapters, foundation files, Program.cs) — fast check while iterating | `dotnet test tests/ArchitectLuna.Template.Tests` (in-memory snapshot tier, sub-second) |
| Templates, adapters, persistence, CLI, generated-output shape — before delivering | `dotnet test ArchitectLuna.sln` (includes the E2E suite that scaffolds, generates, and builds real solutions across the adapter × persistence matrix, plus a `dotnet test` of a generated solution's own test suite) |
| UI | `dotnet build` + manual smoke: `dotnet run --project src/ArchitectLuna.Ui` |

Then deliver:

1. Update docs whose statements your change made false (`README.md`, `docs/ARCHITECTURE.md`,
   `docs/ROADMAP.md` — move completed items to **Done**).
2. For L changes, fill in the plan's **Outcome** section: what shipped, what deviated from the
   plan and why, follow-ups discovered.
3. Commit with a message that describes the *behavior* change, referencing the plan doc if one
   exists. Small, coherent commits over one mega-commit.
4. Push to the designated feature branch. Never commit directly to `main`.
