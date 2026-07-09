using ArchitectLuna.Cli.Adapters;
using ArchitectLuna.Core.Generation;
using ArchitectLuna.Core.Manifest;
using ArchitectLuna.Core.Model;
using ArchitectLuna.Core.Validation;
using ArchitectLuna.Core.Yaml;
using Spectre.Console;

namespace ArchitectLuna.Cli.Scaffolding;

/// <summary>
/// The single generation pipeline every command that produces or refreshes generated files runs
/// through: `generate`, `add field`, and `sync entity` all call <see cref="Run"/> so a field added
/// to an existing entity gets its dependent commands/queries/validators/mappings/handlers/
/// persistence config regenerated the exact same way a plain `generate` run would — there is no
/// separate "sync" code path to keep in step with this one (docs/requirements/003-improvements.md
/// §2.1, §7).
/// </summary>
public static class GenerationRunner
{
    public static bool Run(string root, string modelPath, bool format)
    {
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

            return false;
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

        if (format)
        {
            SolutionScaffolder.TryRunDotnetFormat(root, model.SolutionName);
        }

        AnsiConsole.MarkupLineInterpolated($"[green]Generated {fileCount} file(s) using the '{model.Adapter}' adapter (persistence: {model.Persistence}).[/]");
        return true;
    }
}
