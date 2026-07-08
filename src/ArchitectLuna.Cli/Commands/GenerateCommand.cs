using ArchitectLuna.Cli.Adapters;
using ArchitectLuna.Core.Generation;
using ArchitectLuna.Core.Manifest;
using ArchitectLuna.Core.Validation;
using ArchitectLuna.Core.Workspace;
using ArchitectLuna.Core.Yaml;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ArchitectLuna.Cli.Commands;

public sealed class GenerateCommandSettings : CommandSettings
{
}

public sealed class GenerateCommand : Command<GenerateCommandSettings>
{
    protected override int Execute(CommandContext context, GenerateCommandSettings settings, CancellationToken cancellationToken)
    {
        var root = WorkspaceLocator.Locate(Directory.GetCurrentDirectory());
        var modelPath = Path.Combine(root, ".architect", "model.yaml");
        var manifestPath = Path.Combine(root, ".architect", "manifest.json");

        var model = ModelSerializer.Load(modelPath);
        var validation = ModelValidator.Validate(model);
        if (!validation.IsValid)
        {
            AnsiConsole.MarkupLine("[red]model.yaml is invalid:[/]");
            foreach (var error in validation.Errors)
            {
                AnsiConsole.MarkupLineInterpolated($"  [red]- {error}[/]");
            }

            return 1;
        }

        var adapter = AdapterRegistry.Resolve(model.Adapter);
        var generationContext = new GenerationContext(model.Namespace, $"src/{model.SolutionName}.Api");
        var manifest = ManifestStore.Load(manifestPath);

        var fileCount = 0;
        foreach (var feature in model.Features)
        {
            foreach (var command in feature.Commands)
            {
                foreach (var file in adapter.GenerateCommand(generationContext, feature, command))
                {
                    FileWriter.Write(root, file, manifest);
                    fileCount++;
                }
            }

            foreach (var query in feature.Queries)
            {
                foreach (var file in adapter.GenerateQuery(generationContext, feature, query))
                {
                    FileWriter.Write(root, file, manifest);
                    fileCount++;
                }
            }
        }

        ManifestStore.Save(manifestPath, manifest);

        AnsiConsole.MarkupLineInterpolated($"[green]Generated {fileCount} file(s) using the '{model.Adapter}' adapter.[/]");
        return 0;
    }
}
