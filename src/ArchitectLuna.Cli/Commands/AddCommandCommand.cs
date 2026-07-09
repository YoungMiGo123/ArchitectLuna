using System.ComponentModel;
using ArchitectLuna.Cli.Parsing;
using ArchitectLuna.Core.Model;
using ArchitectLuna.Core.Workspace;
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

public sealed class AddCommandCommand : Command<AddCommandCommandSettings>
{
    protected override int Execute(CommandContext context, AddCommandCommandSettings settings, CancellationToken cancellationToken)
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

        if (feature.Commands.Any(c => c.Name == settings.Name))
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Command '{settings.Name}' already exists in feature '{settings.Feature}'.[/]");
            return 1;
        }

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

        if (kind == CommandKind.Delete && settings.Fields.Length > 0)
        {
            AnsiConsole.MarkupLine("[red]A delete command takes no --field values — it only binds the id from the route.[/]");
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

        if (kind == CommandKind.Delete)
        {
            fields.Add(new FieldModel { Name = "Id", Type = "Guid" });
        }
        else if (kind == CommandKind.Update && fields.All(f => f.Name != "Id"))
        {
            fields.Insert(0, new FieldModel { Name = "Id", Type = "Guid" });
        }

        feature.Commands.Add(new CommandModel { Name = settings.Name, Kind = kind, Fields = fields });
        ModelSerializer.Save(modelPath, model);

        AnsiConsole.MarkupLineInterpolated($"[green]Added command '{settings.Name}' to feature '{settings.Feature}'.[/]");
        return 0;
    }
}
