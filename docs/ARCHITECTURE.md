# Architecture

## Pipeline

```
.architect/model.yaml → ArchitectModel (IR) → validate → IFrameworkAdapter → Scriban render
  → protected-region merge against existing files → write → .architect/manifest.json
```

1. **Intent Model** — `.architect/model.yaml`, a small YAML document: solution name, root
   namespace, chosen adapter, and a list of features. Each feature holds entities, commands, and
   queries. It is hand-edited indirectly, through the `add feature` / `add entity` / `add
   command` / `add query` CLI commands — see `ArchitectLuna.Core/Model/*.cs`.
2. **Entity → CRUD synthesis** — `add entity` is the primary way commands/queries get created.
   `CrudSynthesizer` (`ArchitectLuna.Core/Model/CrudSynthesizer.cs`) expands one `EntityModel`
   into the standard Create/Update/Delete commands and GetById/GetAll queries, so the model is
   built outward from entities rather than assembled command-by-command. `add command`/`add
   query` remain available for anything a standard CRUD shape doesn't cover.
3. **Validation** — `ModelValidator` checks the model before generation (known adapter, no
   duplicate feature/command/query/field names) and fails fast with a readable error list.
4. **Adapter dispatch** — `IFrameworkAdapter` (`ArchitectLuna.Core/Generation/IFrameworkAdapter.cs`)
   is the single seam between "what the model says" and "what code gets written." `MediatRAdapter`
   and `WolverineAdapter` both implement it. Given a `FeatureModel` + `CommandModel`/`QueryModel`,
   an adapter returns a list of `GeneratedFile` (relative path + rendered content) — it does no
   file I/O itself.
5. **Route inference** — `RouteInference` (`ArchitectLuna.Core/Naming/RouteInference.cs`) is
   shared by both adapters so the same model always produces the same route shape regardless of
   `--adapter`: `POST /api/{feature}` for Create, `PUT`/`DELETE /api/{feature}/{id}` for
   Update/Delete, `GET /api/{feature}/{id}` for a single-id-param query, `GET /api/{feature}` for
   a zero-param (list) query.
6. **Templates** — `.sbn` (Scriban) files embedded as resources in `ArchitectLuna.Templates`.
   `Templates/MediatR` and `Templates/Wolverine` hold the framework-specific pieces (the message
   record's base interface, the handler shape); `Templates/Shared` holds the command/query
   endpoint and the FluentValidation validator — both adapters render endpoints through the exact
   same templates, so the generated HTTP surface (minimal-API `IEndpointDefinition` mapping, verbs,
   route binding) is identical no matter which adapter produced it. Only the injected dispatcher
   (`ISender` vs `IMessageBus`) and the handler internals differ.
7. **Protected-region merge** — `ProtectedRegionMerger` scans an existing file for
   `// <architect:region name="...">...// </architect:region>` blocks and splices their content
   into the freshly rendered file before writing, so hand-written logic inside a handler body
   survives regeneration even as the surrounding scaffolding (usings, class name, signature)
   stays in sync with the model.
8. **Write + manifest** — `FileWriter` performs the merge-then-write for each `GeneratedFile` and
   records every path ever generated in `.architect/manifest.json`, laying groundwork for future
   cleanup tooling (e.g. detecting files that used to be generated but no longer are).

## Component map

| Project | Responsibility |
|---|---|
| `ArchitectLuna.Core` | Intent Model, naming/route inference, validation, protected-region merge, manifest, `IFrameworkAdapter` contract. No knowledge of MediatR/Wolverine/Scriban. |
| `ArchitectLuna.Templates` | Scriban engine wrapper + embedded `.sbn` templates. No knowledge of the CLI or the Intent Model's YAML shape. |
| `ArchitectLuna.Adapters.MediatR` / `ArchitectLuna.Adapters.Wolverine` | Implement `IFrameworkAdapter`; each depends only on `Core` and `Templates`, not on each other, so a third-party adapter can be added the same way without touching existing ones (see `docs/ROADMAP.md` M8). |
| `ArchitectLuna.Cli` | Spectre.Console.Cli entry point (`new`, `add feature/entity/command/query`, `generate`) plus `SolutionScaffolder`, which shells out to the real `dotnet` CLI for `.sln`/project creation and package references so version resolution always comes from the live NuGet feed. |

## Why Scriban, and a known environment gotcha

Templates are authored in [Scriban](https://github.com/scriban/scriban), a sandboxed templating
language with no ambient file/network access — the only way a template can affect its output is
through the view model it's given. `TemplateEngine` renders with `StandardMemberRenamer`, so a
C# `CommandName` property is referenced in a template as `{{ command_name }}`.

One gotcha worth knowing if you add new `.sbn` files: MSBuild infers satellite-assembly cultures
from filenames, and a file like `Handler.cs.sbn` gets misread as culture `cs` (Czech) — the
resource silently ends up in a separate satellite assembly instead of the main one, and
`GetManifestResourceNames()` on the main assembly returns nothing at runtime. The fix, already
applied in `ArchitectLuna.Templates.csproj`, is `WithCulture="false"` on the `EmbeddedResource`
item.

## Why shell out to `dotnet` for scaffolding

`new api` calls `dotnet new sln`, `dotnet sln add`, and `dotnet add package` as real subprocesses
rather than hand-writing `.sln`/`.csproj` XML with pinned package versions. This means a scaffolded
solution always resolves current NuGet versions and always has a `.sln`/`.slnx` MSBuild itself
considers valid, at the cost of requiring the `dotnet` CLI to be on `PATH` when running
`architect-luna new api`.
