# Checklist: Add a new messaging adapter

Complexity: **L** — full plan document required (`docs/workflow/feature-workflow.md` §Stage 3).
Reference implementations: `ArchitectLuna.Adapters.MediatR`, `ArchitectLuna.Adapters.Wolverine`.

An adapter owns *how a request is dispatched and handled*; it must not change the HTTP surface —
endpoints and validators come from `Templates/Shared` and routes from `RouteInference`, identical
across adapters.

## Steps

- [ ] **New project** `src/ArchitectLuna.Adapters.<Name>/ArchitectLuna.Adapters.<Name>.csproj`,
      referencing only `ArchitectLuna.Core` and `ArchitectLuna.Templates` (never another adapter).
      Add it to `ArchitectLuna.sln`.
- [ ] **Templates**: add `Templates/<Name>/Message.cs.sbn` and `Templates/<Name>/Handler.cs.sbn`
      in `ArchitectLuna.Templates`. Gotchas (see `docs/ARCHITECTURE.md`):
      - Embedded resource items need `WithCulture="false"` (already set project-wide — confirm the
        glob covers your new folder).
      - `Handle` must be `async` unconditionally and accept a `CancellationToken` in whatever way
        the framework requires *explicitly*, so persistence-backed bodies compile.
- [ ] **Adapter class** `<Name>Adapter : IFrameworkAdapter`, constructor taking
      `IPersistenceGenerator?`. Render messages/handlers from your templates, endpoints/validators
      from `Templates/Shared`. Return `GeneratedFile` records — no file I/O.
- [ ] **Persistence binding**: before rendering a handler, request a `HandlerBinding` from the
      persistence generator (mirror how the existing adapters do it) so real CRUD bodies and the
      single injected dependency work with every provider.
- [ ] **Registry**: add the name to `AdapterRegistry.KnownAdapters` and a case to
      `AdapterRegistry.Resolve` (`src/ArchitectLuna.Cli/Adapters/AdapterRegistry.cs`). Add the
      CLI project reference.
- [ ] **Scaffolding**: teach `SolutionScaffolder` the NuGet package(s) a scaffolded API needs for
      this framework, and any `Program.cs` registration lines the framework requires.
- [ ] **Tests**:
      - `ArchitectLuna.EndToEnd.Tests/GeneratedSolutionBuildTests.cs`: add `InlineData` rows for
        `<name>` × every persistence provider.
      - Check `ProtectedRegionRegenerationTests` covers the new adapter's handler template.
- [ ] **CI**: add the adapter to the smoke-matrix axis in `.github/workflows/ci.yml`.
- [ ] **Docs**: README (adapter list, `--adapter` values), `docs/ARCHITECTURE.md` component map,
      `docs/ROADMAP.md`.

## Definition of done

`dotnet test ArchitectLuna.sln` green, including new matrix rows: a scaffolded solution with the
new adapter **builds** for every persistence provider, and its generated routes are byte-identical
to the other adapters' for the same model.
