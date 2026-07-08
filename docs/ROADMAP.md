# Roadmap

## Done

- **M1 — `new api` skeleton scaffolding.** Shells out to real `dotnet new sln`/`sln add`/`add
  package` so the scaffolded solution always resolves live NuGet versions and has a valid
  `.sln`/`.slnx`.
- **M2 — Intent Model + `add feature`/`add command`/`add query` + `generate`.** YAML model,
  Scriban rendering, protected-region-aware file writer, manifest.
- **M2.5 — `IFrameworkAdapter` abstraction + Wolverine adapter.** Both adapters share route
  inference and the endpoint/validator templates; only message/handler shape and dispatch differ.
- **M3 — Manifest + protected regions**, verified with a real hand-edit-then-regenerate round trip.
- **M4 — Compiling end to end.** Both adapters verified by actually scaffolding, generating, and
  `dotnet build`-ing a sample solution — not just unit tests.
- **Entity-driven CRUD.** `add entity` synthesizes Create/Update/Delete + GetById/GetAll from one
  entity definition (`CrudSynthesizer`), with real REST verbs/routes (`POST`/`PUT /{id}`/`DELETE
  /{id}`/`GET /{id}`/`GET` collection) and GetById/GetAll returning actual entity data instead of
  echoing the lookup key.

## Near-term — get to a demoable prototype

- **Entities → EF Core.** Generate a `DbContext`, entity configuration, and migrations from
  `EntityModel`, and wire generated handlers to real persistence instead of
  `throw new NotImplementedException()`. This is what turns the current scaffold-and-compile
  prototype into something that actually stores data.
- **`invero doctor` / `--verify`.** Run `dotnet build` after `generate` and map errors back to the
  offending `model.yaml` entry, so a bad field type or a naming collision surfaces immediately
  instead of at the next manual build.
- **`adapter switch`.** Regenerate an existing model onto a different adapter in place — the
  proof that `IFrameworkAdapter` is a real seam, not just a naming convention.

## Medium-term

- **Clean Architecture layering as an alternative to vertical slice.** Today's `new api` produces
  a single project with a `Features/` vertical slice per command/query. A `--layout
  clean-architecture` (or similar) option would instead split generated code across
  Domain/Application/Infrastructure/Api projects, sharing the same Intent Model and adapters —
  vertical slice and Clean Architecture become two output shapes over one model, not two
  disconnected tools.
- **A UI layer directly over `ArchitectLuna.Core`.** Core is already free of console I/O — no
  `Console.*` calls outside `ArchitectLuna.Cli`. Keeping that boundary intact is what makes a
  future desktop/web UI (model editing, live preview of generated files, one-click `generate`)
  possible without a rewrite.
- **`SchemaVersion` migration.** `ArchitectModel.SchemaVersion` exists but nothing reads it yet;
  once the YAML shape needs to change, this is what upgrades an older `model.yaml` in place.

## Longer-term

- **FastEndpoints adapter**, then a non-.NET adapter (NestJS or Spring) as the real test of
  whether `IFrameworkAdapter`'s language-agnostic parts of the design (route inference, CRUD
  synthesis) hold up outside .NET.
- **CI pipeline** (build + test on PR, `dotnet pack` + publish `architect-luna` as a global tool).
  Deliberately deferred until the near-term items above are proven, per the original build
  handoff — no CI before the happy path is solid.
