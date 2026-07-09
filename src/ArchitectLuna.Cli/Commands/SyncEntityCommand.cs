using System.ComponentModel;
using ArchitectLuna.Cli.Scaffolding;
using ArchitectLuna.Core.Yaml;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ArchitectLuna.Cli.Commands;

public sealed class SyncEntityCommandSettings : CommandSettings
{
    [CommandArgument(0, "<feature>")]
    public required string Feature { get; init; }

    [CommandArgument(1, "<entity>")]
    public required string Entity { get; init; }

    [CommandOption("--no-format")]
    [Description("Skip running 'dotnet format' over the solution after regeneration.")]
    [DefaultValue(false)]
    public bool NoFormat { get; init; }
}

/// <summary>
/// docs/requirements/003-improvements.md §7: re-syncs an existing entity's dependent artifacts
/// from the current model — useful after a manual `.architect/model.yaml` edit. This is a
/// documented alias, not a distinct pipeline: `generate` already regenerates every entity's
/// dependent artifacts from the model on every run (see <see cref="GenerationRunner"/>), so this
/// command's only job beyond that is confirming the named feature/entity actually exists before
/// running the same pipeline `generate` would.
/// </summary>
public sealed class SyncEntityCommand : Command<SyncEntityCommandSettings>
{
    protected override int Execute(CommandContext context, SyncEntityCommandSettings settings, CancellationToken cancellationToken)
    {
        if (!WorkspaceGuard.TryLocateModelPath(out var modelPath))
        {
            return 1;
        }

        var model = ModelSerializer.Load(modelPath);
        var feature = model.Features.FirstOrDefault(f => f.Name == settings.Feature);
        if (feature is null)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Feature '{settings.Feature}' does not exist.[/]");
            return 1;
        }

        if (feature.Entities.All(e => e.Name != settings.Entity))
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Entity '{settings.Entity}' does not exist in feature '{settings.Feature}'.[/]");
            return 1;
        }

        var root = Path.GetDirectoryName(Path.GetDirectoryName(modelPath))!;
        return GenerationRunner.Run(root, modelPath, format: !settings.NoFormat) ? 0 : 1;
    }
}
