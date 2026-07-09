---
name: feature
description: Structured feature-development workflow with automatic planning. Use for any feature request, enhancement, or non-trivial change to ArchitectLuna — classifies complexity, auto-generates a plan document for complex work, implements step-by-step, and verifies against the adapter × persistence matrix. Invoke with the feature description as the argument, e.g. /feature add an `adapter switch` command. Pass --plan-only to stop after producing the plan for review.
---

# /feature — structured feature development

You are executing the repository's standard feature workflow, defined in
`docs/workflow/feature-workflow.md`. **Read that file now** — it is the authority; this skill is
the driver that applies it. The feature request is: `$ARGUMENTS`.

## Procedure

1. **Intake.** Restate the goal in 1–2 sentences to the user. Read `docs/ARCHITECTURE.md` and
   check `docs/ROADMAP.md` — if the request matches a planned item, adopt its described shape.
   Explore the code the change touches before deciding anything.

2. **Classify** the change S / M / L using the rubric in the workflow doc (Stage 2), including
   the automatic-L triggers. State the classification and the one-line reason. When in doubt,
   round up.

3. **Plan.**
   - **S:** create a short task list (TaskCreate) and go to step 4.
   - **M:** write the plan sections (affected components, design decisions, ordered steps, test
     plan, out-of-scope) inline in your response, then proceed.
   - **L:** copy `docs/workflow/templates/feature-plan.md` to `docs/plans/NNN-<slug>.md` (next
     number; register it in the table in `docs/plans/README.md`), complete **every** section, and
     run the invariant checklist. If a checklist in `docs/workflow/checklists/` matches the
     change type (new adapter, new persistence provider, CLI command, template change), its steps
     become your plan's steps — do not improvise a parallel process.
   - If `$ARGUMENTS` contains `--plan-only`, stop here and present the plan for review.
   - If the request is genuinely ambiguous on a decision that changes the design (not a detail
     you can default sensibly), ask the user before implementing — with the plan as context.

4. **Implement** the plan in step order, one task per plan step, marking tasks in progress /
   completed as you go. After each step run `dotnet build ArchitectLuna.sln`. If the plan turns
   out wrong, **edit the plan document first**, then continue — the doc must match reality.

5. **Verify** per the matrix in the workflow doc (Stage 5): fast suite for Core changes, full
   `dotnet test ArchitectLuna.sln` whenever templates/adapters/persistence/CLI or generated
   output changed. Fix failures; never report done with red tests.

6. **Deliver.**
   - Update any docs your change made stale (`README.md`, `docs/ARCHITECTURE.md`,
     `docs/ROADMAP.md` — move finished roadmap items to Done).
   - For L: fill in the plan's **Outcome** section and set its status in `docs/plans/README.md`.
   - Commit (small, behavior-describing messages; reference the plan doc) and push to the
     session's designated feature branch.
   - Summarize: what shipped, how it was verified, deviations from plan, follow-ups.

## Hard rules

- Never skip the plan for M/L work, even if the implementation seems obvious.
- Never violate the six invariants in the workflow doc (Stage 4) without flagging it explicitly.
- The E2E suite is the ground truth for anything touching generated output — a green build of
  ArchitectLuna itself proves nothing about the code it generates.
