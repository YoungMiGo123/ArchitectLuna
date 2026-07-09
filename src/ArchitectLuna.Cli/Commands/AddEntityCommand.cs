using ArchitectLuna.Cli.Parsing;
using ArchitectLuna.Core.Editing;
using ArchitectLuna.Core.Model;
using ArchitectLuna.Core.Yaml;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ArchitectLuna.Cli.Commands;

public sealed class AddEntityCommandSettings : CommandSettings
{
    [CommandArgument(0, "<feature>")]
    public required string Feature { get; init; }

    [CommandArgument(1, "<name>")]
    public required string Name { get; init; }

    [CommandOption("--field")]
    public string[] Fields { get; init; } = Array.Empty<string>();

    [CommandOption("--rule")]
    public string[] Rules { get; init; } = Array.Empty<string>();
}

/// <summary>
/// An entity is the source of truth for a feature's domain data — everything downstream
/// (commands, queries, handlers, validators, endpoints) is generated outward from it via
/// <see cref="CrudSynthesizer"/>, giving a full Create/Read/Update/Delete/List slice from one call.
/// Ordering and duplicate rules live in <see cref="ModelEditor"/>; this command only parses input
/// and presents outcomes.
/// </summary>
public sealed class AddEntityCommand : Command<AddEntityCommandSettings>
{
    protected override int Execute(CommandContext context, AddEntityCommandSettings settings, CancellationToken cancellationToken)
    {
        if (!WorkspaceGuard.TryLocateModelPath(out var modelPath))
        {
            return 1;
        }

        var model = ModelSerializer.Load(modelPath);

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

        var entity = new EntityModel { Name = settings.Name, Fields = fields };
        var result = ModelEditor.AddEntity(model, settings.Feature, entity);
        if (!result.Success)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{result.Error}[/]");
            return 1;
        }

        ModelSerializer.Save(modelPath, model);
        AnsiConsole.MarkupLineInterpolated($"[green]Added entity '{settings.Name}' to feature '{settings.Feature}' with full CRUD: {string.Join(", ", result.AddedOperations)}.[/]");
        return 0;
    }
}
