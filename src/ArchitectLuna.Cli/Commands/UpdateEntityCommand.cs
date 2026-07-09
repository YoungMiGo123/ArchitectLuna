using System.ComponentModel;
using ArchitectLuna.Cli.Parsing;
using ArchitectLuna.Cli.Scaffolding;
using ArchitectLuna.Core.Editing;
using ArchitectLuna.Core.Yaml;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ArchitectLuna.Cli.Commands;

public sealed class UpdateEntityCommandSettings : CommandSettings
{
    [CommandArgument(0, "<feature>")]
    public required string Feature { get; init; }

    [CommandArgument(1, "<entity>")]
    public required string Entity { get; init; }

    [CommandOption("--add-field")]
    [Description("Name:Type, e.g. Reference:string. Repeatable.")]
    public string[] AddFields { get; init; } = Array.Empty<string>();

    [CommandOption("--no-format")]
    [Description("Skip running 'dotnet format' over the solution after regeneration.")]
    [DefaultValue(false)]
    public bool NoFormat { get; init; }
}

/// <summary>
/// docs/requirements/003-improvements.md §2.1: the `update entity --add-field` spelling of
/// <see cref="AddFieldCommand"/> ("either command style is acceptable, but the behaviour must be
/// supported"). Delegates to the exact same <see cref="ModelEditor.AddFieldToEntity"/> +
/// <see cref="GenerationRunner"/> pipeline.
/// </summary>
public sealed class UpdateEntityCommand : Command<UpdateEntityCommandSettings>
{
    protected override int Execute(CommandContext context, UpdateEntityCommandSettings settings, CancellationToken cancellationToken)
    {
        if (settings.AddFields.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]Specify at least one --add-field Name:Type.[/]");
            return 1;
        }

        if (!WorkspaceGuard.TryLocateModelPath(out var modelPath))
        {
            return 1;
        }

        var model = ModelSerializer.Load(modelPath);

        foreach (var fieldSpec in settings.AddFields)
        {
            var field = SpecParser.ParseField(fieldSpec);
            var result = ModelEditor.AddFieldToEntity(model, settings.Feature, settings.Entity, field);
            if (!result.Success)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]{result.Error}[/]");
                return 1;
            }

            AnsiConsole.MarkupLineInterpolated($"[green]Added field '{field.Name}' to entity '{settings.Entity}' in feature '{settings.Feature}'.[/]");
        }

        ModelSerializer.Save(modelPath, model);

        var root = Path.GetDirectoryName(Path.GetDirectoryName(modelPath))!;
        return GenerationRunner.Run(root, modelPath, format: !settings.NoFormat) ? 0 : 1;
    }
}
