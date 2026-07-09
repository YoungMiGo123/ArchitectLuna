using ArchitectLuna.Cli.Adapters;
using ArchitectLuna.Core.Generation;
using ArchitectLuna.Core.Manifest;
using ArchitectLuna.Core.Model;
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
        if (!WorkspaceGuard.TryLocateModelPath(out var modelPath))
        {
            return 1;
        }

        var root = Path.GetDirectoryName(Path.GetDirectoryName(modelPath))!;
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

        var persistence = PersistenceRegistry.Resolve(model.Persistence);
        var adapter = AdapterRegistry.Resolve(model.Adapter, persistence);
        var generationContext = model.Layout == SolutionLayout.CleanArchitecture
            ? GenerationContext.ForCleanArchitecture(
                model.Namespace,
                $"src/{model.SolutionName}.Api",
                $"src/{model.SolutionName}.Application",
                $"src/{model.SolutionName}.Domain",
                $"src/{model.SolutionName}.Infrastructure",
                $"src/{model.SolutionName}.Contracts")
            : GenerationContext.ForVerticalSlice(model.Namespace, $"src/{model.SolutionName}.Api");
        var manifest = ManifestStore.Load(manifestPath);

        var fileCount = 0;
        var allEntities = new List<EntityReference>();

        foreach (var feature in model.Features)
        {
            foreach (var entity in feature.Entities)
            {
                allEntities.Add(new EntityReference(feature, entity));
                foreach (var file in persistence.GenerateEntityPersistence(generationContext, feature, entity))
                {
                    FileWriter.Write(root, file, manifest);
                    fileCount++;
                }
            }

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

        foreach (var file in persistence.GenerateSolutionPersistence(generationContext, allEntities))
        {
            FileWriter.Write(root, file, manifest);
            fileCount++;
        }

        ManifestStore.Save(manifestPath, manifest);

        AnsiConsole.MarkupLineInterpolated($"[green]Generated {fileCount} file(s) using the '{model.Adapter}' adapter (persistence: {model.Persistence}).[/]");
        return 0;
    }
}
