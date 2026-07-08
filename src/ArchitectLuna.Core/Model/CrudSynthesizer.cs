using ArchitectLuna.Core.Naming;

namespace ArchitectLuna.Core.Model;

/// <summary>
/// Expands an <see cref="EntityModel"/> into the standard Create/Update/Delete commands and
/// GetById/GetAll queries. This is the "entity outwards" rule for the whole tool: an entity is
/// the source of truth, and its commands/queries — and everything the adapters render from them
/// (handlers, validators, endpoints) — are derived from it, not hand-authored independently.
/// </summary>
public static class CrudSynthesizer
{
    public static (List<CommandModel> Commands, List<QueryModel> Queries) SynthesizeCrud(EntityModel entity)
    {
        var idParam = new ParamModel { Name = "Id", Type = "Guid" };
        var resultFields = new List<ParamModel> { idParam }
            .Concat(entity.Fields.Select(f => new ParamModel { Name = f.Name, Type = f.Type }))
            .ToList();

        var createCommand = new CommandModel
        {
            Name = $"Create{entity.Name}",
            Kind = CommandKind.Create,
            Fields = CloneFields(entity.Fields),
        };

        var updateCommand = new CommandModel
        {
            Name = $"Update{entity.Name}",
            Kind = CommandKind.Update,
            Fields = new List<FieldModel> { new() { Name = "Id", Type = "Guid" } }
                .Concat(CloneFields(entity.Fields))
                .ToList(),
        };

        var deleteCommand = new CommandModel
        {
            Name = $"Delete{entity.Name}",
            Kind = CommandKind.Delete,
            Fields = new List<FieldModel> { new() { Name = "Id", Type = "Guid" } },
        };

        var getByIdQuery = new QueryModel
        {
            Name = $"Get{entity.Name}ById",
            Params = new List<ParamModel> { idParam },
            ResultFields = resultFields,
        };

        var getAllQuery = new QueryModel
        {
            Name = $"GetAll{NamingConventions.Pluralize(entity.Name)}",
            Params = new List<ParamModel>(),
            ResultFields = resultFields,
            IsCollection = true,
        };

        return (
            new List<CommandModel> { createCommand, updateCommand, deleteCommand },
            new List<QueryModel> { getByIdQuery, getAllQuery });
    }

    private static List<FieldModel> CloneFields(IEnumerable<FieldModel> fields) =>
        fields.Select(f => new FieldModel { Name = f.Name, Type = f.Type, Rules = new List<string>(f.Rules) }).ToList();
}
