using ArchitectLuna.Cli.Parsing;
using ArchitectLuna.Core.Editing;
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

/// <summary>Bespoke queries are allowed without an entity (Ordering Rule 4) — not every operation is standard CRUD.</summary>
public sealed class AddQueryCommand : Command<AddQueryCommandSettings>
{
    protected override int Execute(CommandContext context, AddQueryCommandSettings settings, CancellationToken cancellationToken)
    {
        if (!WorkspaceGuard.TryLocateModelPath(out var modelPath))
        {
            return 1;
        }

        var model = ModelSerializer.Load(modelPath);

        var parameters = settings.Params.Select(SpecParser.ParseParam).ToList();

        var result = ModelEditor.AddQuery(model, settings.Feature, settings.Name, parameters);
        if (!result.Success)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{result.Error}[/]");
            return 1;
        }

        ModelSerializer.Save(modelPath, model);
        AnsiConsole.MarkupLineInterpolated($"[green]Added query '{settings.Name}' to feature '{settings.Feature}'.[/]");
        return 0;
    }
}
