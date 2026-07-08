# Invero Architect

Invero Architect is a deterministic software-generation tool for .NET. Given a
declarative specification of a system's domain and use cases, it generates
source code — entities, vertical slices, and full Clean Architecture
solutions — where the same input always produces byte-identical output.

It is not an LLM code generator. It is a template-and-model-driven compiler:
`spec in → source code out`, reproducibly, so generated code can be committed,
diffed, and regenerated without surprises.

## Why

Most scaffolding tools (`dotnet new`, Yeoman, Cookiecutter, LLM copilots) are
either one-shot generators you throw away after first use, or nondeterministic
assistants whose output changes run to run. Invero Architect is designed to be
run repeatedly over the life of a project:

- **Deterministic** — identical spec + identical template pack version ⇒
  identical bytes. No timestamps, GUIDs, or randomness leak into output.
- **Regenerable** — running generation again against an evolved spec produces
  a clean diff against hand-authored code, instead of clobbering it.
- **Pattern-pluggable** — Clean Architecture, Vertical Slices, and plain
  entity/DDD scaffolding are separate template packs on a shared engine, not
  hardcoded paths.

## Status

This repository currently holds the planning documents for the project. No
code has been scaffolded yet.

## Documentation

- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — how the generation engine
  works and how determinism is guaranteed.
- [`docs/PATTERNS.md`](docs/PATTERNS.md) — the architecture patterns the
  generator targets (Clean Architecture, Vertical Slices, Entities/DDD,
  generic templates) and what each produces.
- [`docs/ROADMAP.md`](docs/ROADMAP.md) — phased build plan from this planning
  stage through to a working CLI.
