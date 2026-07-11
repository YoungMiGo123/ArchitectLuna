using System.ComponentModel;
using ArchitectLuna.Cli.Parsing;
using ArchitectLuna.Cli.Scaffolding;
using ArchitectLuna.Core.Editing;
using ArchitectLuna.Core.Yaml;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ArchitectLuna.Cli.Commands;

public sealed class AddFieldCommandSettings : CommandSettings
{
    [CommandArgument(0, "<feature>")]
    public required string Feature { get; init; }

    [CommandArgument(1, "<entity>")]
    public required string Entity { get; init; }

    [CommandArgument(2, "<field>")]
    [Description("Name:Type, e.g. Reference:string")]
    public required string Field { get; init; }

    [CommandOption("--no-format")]
    [Description("Skip running 'dotnet format' over the solution after regeneration.")]
    [DefaultValue(false)]
    public bool NoFormat { get; init; }
}

/// <summary>
/// docs/requirements/003-improvements.md §2.1, §7: adds a field to an existing entity and then
/// runs the standard generation pipeline so every dependent artifact (entity class, persistence
/// config, Create/Update commands, GetById/GetAll queries, validators, mappings, handlers) picks
/// up the new field in the same run — see <see cref="ModelEditor.AddFieldToEntity"/> and
/// <see cref="GenerationRunner"/> for why one command covers both the model edit and the sync.
/// </summary>
public sealed class AddFieldCommand : Command<AddFieldCommandSettings>
{
    protected override int Execute(CommandContext context, AddFieldCommandSettings settings, CancellationToken cancellationToken)
    {
        if (!WorkspaceGuard.TryLocateModelPath(out var modelPath))
        {
            return 1;
        }

        var model = ModelSerializer.Load(modelPath);
        var field = SpecParser.ParseField(settings.Field);

        var result = ModelEditor.AddFieldToEntity(model, settings.Feature, settings.Entity, field);
        if (!result.Success)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{result.Error}[/]");
            return 1;
        }

        ModelSerializer.Save(modelPath, model);
        AnsiConsole.MarkupLineInterpolated($"[green]Added field '{field.Name}' to entity '{settings.Entity}' in feature '{settings.Feature}'.[/]");

        var root = Path.GetDirectoryName(Path.GetDirectoryName(modelPath))!;
        return GenerationRunner.Run(root, modelPath, format: !settings.NoFormat) ? 0 : 1;
    }
}
