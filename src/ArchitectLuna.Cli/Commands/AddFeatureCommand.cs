using ArchitectLuna.Core.Editing;
using ArchitectLuna.Core.Yaml;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ArchitectLuna.Cli.Commands;

public sealed class AddFeatureCommandSettings : CommandSettings
{
    [CommandArgument(0, "<name>")]
    public required string Name { get; init; }
}

public sealed class AddFeatureCommand : Command<AddFeatureCommandSettings>
{
    protected override int Execute(CommandContext context, AddFeatureCommandSettings settings, CancellationToken cancellationToken)
    {
        if (!WorkspaceGuard.TryLocateModelPath(out var modelPath))
        {
            return 1;
        }

        var model = ModelSerializer.Load(modelPath);

        var result = ModelEditor.AddFeature(model, settings.Name);
        if (!result.Success)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{result.Error}[/]");
            return 1;
        }

        ModelSerializer.Save(modelPath, model);
        AnsiConsole.MarkupLineInterpolated($"[green]Added feature '{settings.Name}'.[/]");
        return 0;
    }
}
