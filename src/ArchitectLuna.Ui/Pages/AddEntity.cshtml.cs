using ArchitectLuna.Core.Editing;
using ArchitectLuna.Core.Model;
using ArchitectLuna.Core.Validation;
using ArchitectLuna.Core.Yaml;
using ArchitectLuna.Ui.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ArchitectLuna.Ui.Pages;

/// <summary>
/// Uses the same "entity outwards" logic as the CLI's AddEntityCommand — both are thin
/// presenters over ArchitectLuna.Core's ModelEditor, which owns the ordering/duplicate rules —
/// rather than shelling out to the CLI, since Core is meant to be consumed directly by a UI.
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

        // The form only offers one combined "feature" input, so a missing feature is created
        // here instead of failing like the CLI's stricter two-step flow does.
        if (model.Features.All(f => f.Name != FeatureName))
        {
            ModelEditor.AddFeature(model, FeatureName);
        }

        var entity = new EntityModel { Name = EntityName, Fields = fields };
        var editResult = ModelEditor.AddEntity(model, FeatureName, entity);
        if (!editResult.Success)
        {
            ErrorMessage = editResult.Error;
            return;
        }

        var validation = ModelValidator.Validate(model);
        if (!validation.IsValid)
        {
            ErrorMessage = "model.yaml would become invalid: " + string.Join("; ", validation.Errors);
            return;
        }

        var modelPath = Path.Combine(ResolvedRoot!, ".architect", "model.yaml");
        ModelSerializer.Save(modelPath, model);

        SuccessMessage = $"Added entity '{EntityName}' to feature '{FeatureName}' with full CRUD: " +
                          string.Join(", ", editResult.AddedOperations) + ".";

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
