# Architecture

## Pipeline

```
spec (YAML/JSON) → parse → IR (Model) → resolve template pack → render → format → write
```

1. **Spec** — a declarative, hand-authored file describing the domain:
   entities, value objects, aggregates, use cases/commands/queries, and which
   architecture pattern to target. This is the only input that varies between
   runs.
2. **Parse** — the spec is validated against a schema and loaded into an
   in-memory **Intermediate Representation (IR)**. Parsing is pure: no file
   system reads beyond the spec itself, no clock, no environment lookups.
3. **Template pack resolution** — a template pack (e.g.
   `InveroArchitect.Templates.CleanArchitecture`) is selected and pinned by
   version. The pack maps IR nodes to file outputs.
4. **Render** — each output file is produced by a template function of
   `(IR node, pack version) → text`. Templates are pure functions with no
   side effects; they cannot read ambient state.
5. **Format** — all rendered text passes through a single canonical
   formatter pass (pinned `dotnet format`/Roslyn config) so output style is
   independent of the machine or IDE that ran generation.
6. **Write** — files are written in a deterministic, sorted order. The tool
   also supports a dry-run diff mode instead of writing.

## Determinism guarantees

Determinism is the core product requirement, not an implementation detail.
Concretely:

- **No hidden inputs.** Generation is a pure function of `(spec content,
  template pack version)`. No timestamps, machine-generated GUIDs, random
  seeds, or environment variables are permitted in template output unless
  they are explicit, spec-provided values.
- **Canonical ordering.** Every collection that affects output (files,
  members, usings, properties) is sorted deterministically before rendering
  — never emitted in hash-map or file-system enumeration order.
- **Content-addressed template packs.** Each template pack is versioned and
  hashed. A spec pins the exact pack version it was generated with, so
  regenerating months later with the same pin reproduces the same bytes even
  if the pack has since moved on.
- **Idempotent regeneration.** Running generation twice against the same
  spec and pack produces identical output. This is a testable property
  (`invero doctor` runs generation twice and diffs the result) and is part
  of CI for every template pack.
- **Generated vs. hand-authored separation.** Generated files are either
  fully owned by the tool (safe to overwrite every run) or contain clearly
  marked extension points (partial classes, designated regions) so
  regeneration never silently discards developer edits. The tool refuses to
  overwrite a hand-edited region without an explicit merge step.

## Components

| Component | Responsibility |
|---|---|
| `InveroArchitect.Core` | Spec schema, parser, IR model. No knowledge of any specific architecture pattern. |
| `InveroArchitect.Generation` | Rendering pipeline, canonical formatter integration, idempotency/diff checking, file writer. |
| `InveroArchitect.Templates.*` | One package per pattern (Clean Architecture, Vertical Slice, Entities). Each maps IR → files using the shared engine. Independently versioned. |
| `InveroArchitect.Cli` | `invero` command-line entry point: `generate`, `new`, `doctor`, `diff`. |

Template packs depend on `Core` and `Generation`; they do not depend on each
other. This keeps "generic multi-target templates" (a pack authored outside
this repo) possible: anyone can implement the same IR → files contract.

## Template engine choice

Templates are authored with [Scriban](https://github.com/scriban/scriban) (a
sandboxed, side-effect-free .NET templating language) rather than T4 or
runtime code generation. Scriban templates cannot execute arbitrary .NET code
or touch ambient state, which is what makes the purity guarantee in step 4
enforceable rather than just a convention.

## Spec format (sketch)

```yaml
architecture: clean-architecture   # or: vertical-slice, entities-only
namespace: Invero.Sample

entities:
  - name: Order
    properties:
      - { name: Id, type: Guid }
      - { name: CustomerId, type: Guid }
      - { name: Status, type: OrderStatus }
    invariants:
      - "Status transitions only forward: Placed -> Paid -> Shipped -> Delivered"

useCases:
  - name: PlaceOrder
    kind: command
    entity: Order
```

The exact schema will be finalized during Phase 2 (see `ROADMAP.md`); this
sketch establishes the shape: entities and use cases are first-class,
architecture style is a top-level switch, and the spec has no
generation-run-specific fields (no timestamps, no output paths baked in).
