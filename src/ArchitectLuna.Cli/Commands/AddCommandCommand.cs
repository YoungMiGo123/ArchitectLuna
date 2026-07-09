using System.ComponentModel;
using ArchitectLuna.Cli.Parsing;
using ArchitectLuna.Core.Editing;
using ArchitectLuna.Core.Model;
using ArchitectLuna.Core.Yaml;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ArchitectLuna.Cli.Commands;

public sealed class AddCommandCommandSettings : CommandSettings
{
    [CommandArgument(0, "<feature>")]
    public required string Feature { get; init; }

    [CommandArgument(1, "<name>")]
    public required string Name { get; init; }

    [CommandOption("--field")]
    public string[] Fields { get; init; } = Array.Empty<string>();

    [CommandOption("--rule")]
    public string[] Rules { get; init; } = Array.Empty<string>();

    [CommandOption("--kind")]
    [Description("create, update, or delete. Update/delete commands route to PUT/DELETE .../{id} and bind the id from the route.")]
    [DefaultValue("create")]
    public string Kind { get; init; } = "create";
}

/// <summary>Bespoke commands are allowed without an entity (Ordering Rule 4) — not every operation is standard CRUD.</summary>
public sealed class AddCommandCommand : Command<AddCommandCommandSettings>
{
    protected override int Execute(CommandContext context, AddCommandCommandSettings settings, CancellationToken cancellationToken)
    {
        if (!WorkspaceGuard.TryLocateModelPath(out var modelPath))
        {
            return 1;
        }

        var model = ModelSerializer.Load(modelPath);

        CommandKind kind;
        try
        {
            kind = SpecParser.ParseKind(settings.Kind);
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{ex.Message}[/]");
            return 1;
        }

        var fields = settings.Fields.Select(SpecParser.ParseField).ToList();

        foreach (var ruleSpec in settings.Rules)
        {
            var (fieldName, rule) = SpecParser.ParseRule(ruleSpec);
            var field = fields.FirstOrDefault(f => f.Name == fieldName);
            if (field is null)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]--rule references unknown field '{fieldName}'. Declare it with --field first.[/]");
                return 1;
            }

            field.Rules.Add(rule);
        }

        var result = ModelEditor.AddCommand(model, settings.Feature, settings.Name, kind, fields);
        if (!result.Success)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{result.Error}[/]");
            return 1;
        }

        ModelSerializer.Save(modelPath, model);
        AnsiConsole.MarkupLineInterpolated($"[green]Added command '{settings.Name}' to feature '{settings.Feature}'.[/]");
        return 0;
    }
}
