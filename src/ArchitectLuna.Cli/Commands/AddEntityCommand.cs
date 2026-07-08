using ArchitectLuna.Cli.Parsing;
using ArchitectLuna.Core.Model;
using ArchitectLuna.Core.Workspace;
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
/// </summary>
public sealed class AddEntityCommand : Command<AddEntityCommandSettings>
{
    protected override int Execute(CommandContext context, AddEntityCommandSettings settings, CancellationToken cancellationToken)
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

        if (feature.Entities.Any(e => e.Name == settings.Name))
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Entity '{settings.Name}' already exists in feature '{settings.Feature}'.[/]");
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

        var entity = new EntityModel { Name = settings.Name, Fields = fields };
        var (commands, queries) = CrudSynthesizer.SynthesizeCrud(entity);

        var collisions = commands.Select(c => c.Name).Where(n => feature.Commands.Any(c => c.Name == n))
            .Concat(queries.Select(q => q.Name).Where(n => feature.Queries.Any(q => q.Name == n)))
            .ToList();

        if (collisions.Count > 0)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Cannot add entity '{settings.Name}': it would generate command/query names that already exist in feature '{settings.Feature}': {string.Join(", ", collisions)}.[/]");
            return 1;
        }

        feature.Entities.Add(entity);
        feature.Commands.AddRange(commands);
        feature.Queries.AddRange(queries);
        ModelSerializer.Save(modelPath, model);

        AnsiConsole.MarkupLineInterpolated($"[green]Added entity '{settings.Name}' to feature '{settings.Feature}' with full CRUD: {string.Join(", ", commands.Select(c => c.Name).Concat(queries.Select(q => q.Name)))}.[/]");
        return 0;
    }
}
