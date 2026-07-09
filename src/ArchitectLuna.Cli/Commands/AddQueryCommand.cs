using ArchitectLuna.Cli.Parsing;
using ArchitectLuna.Core.Model;
using ArchitectLuna.Core.Workspace;
using ArchitectLuna.Core.Yaml;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ArchitectLuna.Cli.Commands;

public sealed class AddQueryCommandSettings : CommandSettings
{
    [CommandArgument(0, "<feature>")]
    public required string Feature { get; init; }

    [CommandArgument(1, "<name>")]
    public required string Name { get; init; }

    [CommandOption("--param")]
    public string[] Params { get; init; } = Array.Empty<string>();
}

public sealed class AddQueryCommand : Command<AddQueryCommandSettings>
{
    protected override int Execute(CommandContext context, AddQueryCommandSettings settings, CancellationToken cancellationToken)
    {
        var root = WorkspaceLocator.Locate(Directory.GetCurrentDirectory());
        var modelPath = Path.Combine(root, ".architect", "model.yaml");
        var model = ModelSerializer.Load(modelPath);

        var feature = model.Features.FirstOrDefault(f => f.Name == settings.Feature);
        if (feature is null)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Feature '{settings.Feature}' does not exist. Run 'add feature {settings.Feature}' first.[/]");
            return 1;
        }

        if (feature.Queries.Any(q => q.Name == settings.Name))
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Query '{settings.Name}' already exists in feature '{settings.Feature}'.[/]");
            return 1;
        }

        var parameters = settings.Params.Select(SpecParser.ParseParam).ToList();

        feature.Queries.Add(new QueryModel { Name = settings.Name, Params = parameters });
        ModelSerializer.Save(modelPath, model);

        AnsiConsole.MarkupLineInterpolated($"[green]Added query '{settings.Name}' to feature '{settings.Feature}'.[/]");
        return 0;
    }
}
