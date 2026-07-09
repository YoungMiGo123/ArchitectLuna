using ArchitectLuna.Core.Model;
using ArchitectLuna.Core.Validation;
using ArchitectLuna.Core.Yaml;
using ArchitectLuna.Ui.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ArchitectLuna.Ui.Pages;

/// <summary>
/// Replicates the same "entity outwards" logic as the CLI's AddEntityCommand
/// (src/ArchitectLuna.Cli/Commands/AddEntityCommand.cs) directly against ArchitectLuna.Core -
/// ModelSerializer.Load/Save, CrudSynthesizer.SynthesizeCrud, ModelValidator.Validate - rather
/// than shelling out to the CLI, since Core is meant to be consumed directly by a UI.
/// One difference from the CLI command: if the chosen feature doesn't exist yet, this page
/// creates it instead of requiring a separate "add feature" step first, since the form only
/// offers one combined "feature" input.
/// </summary>
public sealed class AddEntityModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Root { get; set; }

    [BindProperty]
    public string? FeatureName { get; set; }

    [BindProperty]
    public string? EntityName { get; set; }

    [BindProperty]
    public List<string> FieldName { get; set; } = new();

    [BindProperty]
    public List<string> FieldType { get; set; } = new();

    public ArchitectModel? LoadedModel { get; private set; }

    public string? ResolvedRoot { get; private set; }

    public string? ErrorMessage { get; private set; }

    public string? SuccessMessage { get; private set; }

    public void OnGet()
    {
        LoadWorkspace();
    }

    public void OnPost()
    {
        var loaded = LoadWorkspace();
        if (loaded is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(FeatureName))
        {
            ErrorMessage = "Feature name is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(EntityName))
        {
            ErrorMessage = "Entity name is required.";
            return;
        }

        var fields = new List<FieldModel>();
        for (var i = 0; i < FieldName.Count; i++)
        {
            var name = FieldName[i]?.Trim();
            var type = i < FieldType.Count ? FieldType[i]?.Trim() : null;
            if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(type))
            {
                continue; // blank row, ignore
            }

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(type))
            {
                ErrorMessage = "Every field row needs both a name and a type.";
                return;
            }

            fields.Add(new FieldModel { Name = name, Type = type });
        }

        var model = loaded;

        var feature = model.Features.FirstOrDefault(f => f.Name == FeatureName);
        if (feature is null)
        {
            feature = new FeatureModel { Name = FeatureName };
            model.Features.Add(feature);
        }

        if (feature.Entities.Any(e => e.Name == EntityName))
        {
            ErrorMessage = $"Entity '{EntityName}' already exists in feature '{FeatureName}'.";
            return;
        }

        var entity = new EntityModel { Name = EntityName, Fields = fields };
        var (commands, queries) = CrudSynthesizer.SynthesizeCrud(entity);

        var collisions = commands.Select(c => c.Name).Where(n => feature.Commands.Any(c => c.Name == n))
            .Concat(queries.Select(q => q.Name).Where(n => feature.Queries.Any(q => q.Name == n)))
            .ToList();

        if (collisions.Count > 0)
        {
            ErrorMessage = $"Cannot add entity '{EntityName}': it would generate command/query names that already exist in feature '{FeatureName}': {string.Join(", ", collisions)}.";
            return;
        }

        feature.Entities.Add(entity);
        feature.Commands.AddRange(commands);
        feature.Queries.AddRange(queries);

        var validation = ModelValidator.Validate(model);
        if (!validation.IsValid)
        {
            ErrorMessage = "model.yaml would become invalid: " + string.Join("; ", validation.Errors);
            return;
        }

        var modelPath = Path.Combine(ResolvedRoot!, ".architect", "model.yaml");
        ModelSerializer.Save(modelPath, model);

        SuccessMessage = $"Added entity '{EntityName}' to feature '{FeatureName}' with full CRUD: " +
                          string.Join(", ", commands.Select(c => c.Name).Concat(queries.Select(q => q.Name))) + ".";

        // Reload so the page reflects the freshly-saved state and the form resets.
        LoadedModel = model;
        FeatureName = null;
        EntityName = null;
        FieldName = new List<string>();
        FieldType = new List<string>();
    }

    private ArchitectModel? LoadWorkspace()
    {
        var result = WorkspaceLoader.Load(Root);
        if (!result.Success)
        {
            ErrorMessage = result.Error;
            return null;
        }

        LoadedModel = result.Model;
        ResolvedRoot = result.RootPath;
        return result.Model;
    }
}
