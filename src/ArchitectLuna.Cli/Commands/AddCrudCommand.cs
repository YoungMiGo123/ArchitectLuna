using ArchitectLuna.Core.Editing;
using ArchitectLuna.Core.Yaml;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ArchitectLuna.Cli.Commands;

public sealed class AddCrudCommandSettings : CommandSettings
{
    [CommandArgument(0, "<feature>")]
    public required string Feature { get; init; }

    [CommandArgument(1, "<entity>")]
    public required string Entity { get; init; }
}

/// <summary>
/// Ordering Rule 3 made explicit: entity-backed CRUD is generated *from* an entity, so this
/// command fails with "create the entity first" guidance when the entity doesn't exist, and
/// synthesizes any missing standard CRUD operations when it does (`add entity` already creates
/// them up front, so this mostly recovers operations that were removed from the model).
/// </summary>
public sealed class AddCrudCommand : Command<AddCrudCommandSettings>
{
    protected override int Execute(CommandContext context, AddCrudCommandSettings settings, CancellationToken cancellationToken)
    {
        if (!WorkspaceGuard.TryLocateModelPath(out var modelPath))
        {
            return 1;
        }

        var model = ModelSerializer.Load(modelPath);

        var result = ModelEditor.AddCrud(model, settings.Feature, settings.Entity);
        if (!result.Success)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{result.Error}[/]");
            return 1;
        }

        if (result.AddedOperations.Count == 0)
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]All standard CRUD operations for '{settings.Entity}' already exist in feature '{settings.Feature}' — nothing to add.[/]");
            return 0;
        }

        ModelSerializer.Save(modelPath, model);
        AnsiConsole.MarkupLineInterpolated($"[green]Added CRUD operations for '{settings.Entity}' in feature '{settings.Feature}': {string.Join(", ", result.AddedOperations)}.[/]");
        return 0;
    }
}
