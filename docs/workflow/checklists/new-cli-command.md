# Checklist: Add or extend a CLI command

Complexity: **M** (new option on existing command) to **L** (new verb that changes the model
schema or generation behavior). Reference: `src/ArchitectLuna.Cli/Commands/*.cs` (Spectre.Console.Cli).

## Steps

- [ ] **Model first**: if the command reads/writes new model state, extend the Intent Model
      classes in `ArchitectLuna.Core/Model/` and `ModelValidator` *before* the command — the CLI
      is a thin shell over Core. Keep YAML round-tripping intact (`ModelSerializerTests`).
- [ ] **Command class** in `src/ArchitectLuna.Cli/Commands/`, registered in `Program.cs`.
      Follow the existing settings-class + validation pattern; reuse `SpecParser` for
      `Field:Type`-style arguments rather than ad-hoc parsing.
- [ ] **No business logic in the command** — commands parse input, call Core, and print results.
      Anything worth unit-testing belongs in Core.
- [ ] **Errors**: fail fast with a readable message listing valid values (match the style of
      `AdapterRegistry.Resolve`'s error).
- [ ] **UI parity check**: if the command edits the model, does `ArchitectLuna.Ui` need the same
      capability? If yes and it's out of scope, record the gap in `docs/ROADMAP.md`.
- [ ] **Tests**: Core logic in `Core.Tests`; if the command affects generated output or scaffolding,
      an E2E test that shells out to the real CLI (see `EndToEnd.Tests/Infrastructure`).
- [ ] **Docs**: README quick-start / command examples; `docs/ROADMAP.md` if this completes a
      planned item.

## Definition of done

`dotnet test ArchitectLuna.sln` green; running the new command against a scratch solution behaves
as documented, and `--help` output reads correctly.
