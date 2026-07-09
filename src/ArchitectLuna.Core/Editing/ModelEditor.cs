using ArchitectLuna.Core.Model;

namespace ArchitectLuna.Core.Editing;

/// <summary>
/// The single owner of Intent Model mutations and their ordering rules (see
/// docs/requirements/001-implementation-architecture.md §Ordering Rules): a feature must exist
/// before entities/commands/queries can be added to it, entity-backed CRUD requires the entity,
/// bespoke commands/queries are allowed without one, and duplicates fail without touching the
/// model. The CLI and UI are thin presenters over these methods, so the rules are enforced (and
/// unit-testable) in one place regardless of which front end performs the edit. A failed edit
/// never partially mutates the model.
/// </summary>
public static class ModelEditor
{
    public static EditResult AddFeature(ArchitectModel model, string name)
    {
        if (model.Features.Any(f => f.Name == name))
        {
            return EditResult.Fail($"Feature '{name}' already exists.");
        }

        model.Features.Add(new FeatureModel { Name = name });
        return EditResult.Ok();
    }

    public static EditResult AddEntity(ArchitectModel model, string featureName, EntityModel entity)
    {
        var feature = model.Features.FirstOrDefault(f => f.Name == featureName);
        if (feature is null)
        {
            return EditResult.Fail($"Feature '{featureName}' does not exist. Run 'architect-luna add feature {featureName}' first.");
        }

        if (feature.Entities.Any(e => e.Name == entity.Name))
        {
            return EditResult.Fail($"Entity '{entity.Name}' already exists in feature '{featureName}'.");
        }

        var (commands, queries) = CrudSynthesizer.SynthesizeCrud(entity);

        var collisions = commands.Select(c => c.Name).Where(n => feature.Commands.Any(c => c.Name == n))
            .Concat(queries.Select(q => q.Name).Where(n => feature.Queries.Any(q => q.Name == n)))
            .ToList();

        if (collisions.Count > 0)
        {
            return EditResult.Fail(
                $"Cannot add entity '{entity.Name}': it would generate command/query names that already exist in feature '{featureName}': {string.Join(", ", collisions)}.");
        }

        feature.Entities.Add(entity);
        feature.Commands.AddRange(commands);
        feature.Queries.AddRange(queries);
        return EditResult.Ok(commands.Select(c => c.Name).Concat(queries.Select(q => q.Name)).ToList());
    }

    /// <summary>
    /// Synthesizes any missing standard CRUD operations for an entity that already exists —
    /// `add entity` already does this at creation time, so this exists for recovering deleted
    /// operations and, more importantly, to fail with actionable guidance when the entity is
    /// missing (Ordering Rule 3: CRUD requires an entity).
    /// </summary>
    public static EditResult AddCrud(ArchitectModel model, string featureName, string entityName)
    {
        var feature = model.Features.FirstOrDefault(f => f.Name == featureName);
        if (feature is null)
        {
            return EditResult.Fail($"Feature '{featureName}' does not exist. Run 'architect-luna add feature {featureName}' first.");
        }

        var entity = feature.Entities.FirstOrDefault(e => e.Name == entityName);
        if (entity is null)
        {
            return EditResult.Fail(
                $"Entity '{entityName}' does not exist in feature '{featureName}'. Create the entity first: " +
                $"architect-luna add entity {featureName} {entityName} --field Name:Type");
        }

        var (commands, queries) = CrudSynthesizer.SynthesizeCrud(entity);
        var missingCommands = commands.Where(c => feature.Commands.All(existing => existing.Name != c.Name)).ToList();
        var missingQueries = queries.Where(q => feature.Queries.All(existing => existing.Name != q.Name)).ToList();

        feature.Commands.AddRange(missingCommands);
        feature.Queries.AddRange(missingQueries);
        return EditResult.Ok(missingCommands.Select(c => c.Name).Concat(missingQueries.Select(q => q.Name)).ToList());
    }

    /// <summary>
    /// Bespoke commands are deliberately allowed without an entity (Ordering Rule 4) — not every
    /// operation is entity-backed. Delete commands take no caller-supplied fields (the id binds
    /// from the route); update commands get an Id field injected when the caller didn't supply one.
    /// </summary>
    public static EditResult AddCommand(ArchitectModel model, string featureName, string name, CommandKind kind, List<FieldModel> fields)
    {
        var feature = model.Features.FirstOrDefault(f => f.Name == featureName);
        if (feature is null)
        {
            return EditResult.Fail($"Feature '{featureName}' does not exist. Run 'architect-luna add feature {featureName}' first.");
        }

        if (feature.Commands.Any(c => c.Name == name))
        {
            return EditResult.Fail($"Command '{name}' already exists in feature '{featureName}'.");
        }

        if (kind == CommandKind.Delete && fields.Count > 0)
        {
            return EditResult.Fail("A delete command takes no --field values — it only binds the id from the route.");
        }

        var resolvedFields = new List<FieldModel>(fields);
        if (kind == CommandKind.Delete)
        {
            resolvedFields.Add(new FieldModel { Name = "Id", Type = "Guid" });
        }
        else if (kind == CommandKind.Update && resolvedFields.All(f => f.Name != "Id"))
        {
            resolvedFields.Insert(0, new FieldModel { Name = "Id", Type = "Guid" });
        }

        feature.Commands.Add(new CommandModel { Name = name, Kind = kind, Fields = resolvedFields });
        return EditResult.Ok();
    }

    /// <summary>Bespoke queries are deliberately allowed without an entity (Ordering Rule 4).</summary>
    public static EditResult AddQuery(ArchitectModel model, string featureName, string name, List<ParamModel> parameters)
    {
        var feature = model.Features.FirstOrDefault(f => f.Name == featureName);
        if (feature is null)
        {
            return EditResult.Fail($"Feature '{featureName}' does not exist. Run 'architect-luna add feature {featureName}' first.");
        }

        if (feature.Queries.Any(q => q.Name == name))
        {
            return EditResult.Fail($"Query '{name}' already exists in feature '{featureName}'.");
        }

        feature.Queries.Add(new QueryModel { Name = name, Params = parameters });
        return EditResult.Ok();
    }
}
