using ArchitectLuna.Core.Model;
using ArchitectLuna.Core.Workspace;
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
        var root = WorkspaceLocator.Locate(Directory.GetCurrentDirectory());
        var modelPath = Path.Combine(root, ".architect", "model.yaml");
        var model = ModelSerializer.Load(modelPath);

        if (model.Features.Any(f => f.Name == settings.Name))
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Feature '{settings.Name}' already exists.[/]");
            return 1;
        }

        model.Features.Add(new FeatureModel { Name = settings.Name });
        ModelSerializer.Save(modelPath, model);

        AnsiConsole.MarkupLineInterpolated($"[green]Added feature '{settings.Name}'.[/]");
        return 0;
    }
}
