# Checklist: Change a Scriban template (`.sbn`)

Complexity: **M** minimum — template changes alter generated output for every user of the
affected adapter(s). Templates live in `src/ArchitectLuna.Templates/Templates/{MediatR,Wolverine,Shared}`.

## Steps

- [ ] **Locate the parity twin.** A change in `Templates/MediatR` almost always needs the
      equivalent in `Templates/Wolverine` (and vice versa). Changes to `Templates/Shared` affect
      both adapters at once — confirm that's intended.
- [ ] **View-model check**: template variables are `snake_case` renderings of C# view-model
      properties (`StandardMemberRenamer`: `CommandName` → `{{ command_name }}`). If you need new
      data, extend the view model in the adapter and pass it — templates cannot reach outside
      what they're given (Scriban is sandboxed; that's deliberate).
- [ ] **New `.sbn` file?** MSBuild will misread `Handler.cs.sbn`-style names as a culture
      satellite unless the `EmbeddedResource` item has `WithCulture="false"` — confirm the
      existing glob in `ArchitectLuna.Templates.csproj` covers your file, or the resource will
      silently vanish at runtime.
- [ ] **Async/cancellation rules**: `Handle` stays unconditionally `async`; Wolverine handlers
      keep the explicit `CancellationToken cancellationToken` parameter. Both exist so
      persistence-backed bodies compile — do not "clean them up".
- [ ] **Protected regions**: if you move, rename, or add `// <architect:region>` markers,
      `ProtectedRegionMerger` must still find and splice user content —
      `ProtectedRegionMergerTests` and `ProtectedRegionRegenerationTests` are the gate.
- [ ] **Inspect real output**: scaffold a scratch solution, run `generate`, and read the emitted
      C# — don't trust the template diff alone.
- [ ] **Tests**: `dotnet test ArchitectLuna.sln` — the E2E suite builds generated output for every
      adapter × persistence combination, which is what actually proves a template change compiles.
- [ ] **Docs**: update README's "What `generate` produces" table if file names/shapes changed.

## Definition of done

Full solution test run green; a manually inspected `generate` output shows the intended change on
**both** adapters (or the plan documents why only one); hand-written handler bodies survive a
regenerate.
