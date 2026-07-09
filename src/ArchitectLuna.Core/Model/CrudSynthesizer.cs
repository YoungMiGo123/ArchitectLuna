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
            EntityName = entity.Name,
        };

        var updateCommand = new CommandModel
        {
            Name = $"Update{entity.Name}",
            Kind = CommandKind.Update,
            Fields = new List<FieldModel> { new() { Name = "Id", Type = "Guid" } }
                .Concat(CloneFields(entity.Fields))
                .ToList(),
            EntityName = entity.Name,
        };

        var deleteCommand = new CommandModel
        {
            Name = $"Delete{entity.Name}",
            Kind = CommandKind.Delete,
            Fields = new List<FieldModel> { new() { Name = "Id", Type = "Guid" } },
            EntityName = entity.Name,
        };

        var getByIdQuery = new QueryModel
        {
            Name = $"Get{entity.Name}ById",
            Params = new List<ParamModel> { idParam },
            ResultFields = resultFields,
            EntityName = entity.Name,
        };

        var getAllQuery = new QueryModel
        {
            Name = $"GetAll{NamingConventions.Pluralize(entity.Name)}",
            // Page/PageSize are bound from the query string (?page=&pageSize=) with defaults, not
            // from the route — the endpoint keeps the plain collection route. They carry the
            // paging arguments into the handler, which returns a PagedResult<T>.
            Params = new List<ParamModel>
            {
                new() { Name = "Page", Type = "int" },
                new() { Name = "PageSize", Type = "int" },
            },
            ResultFields = resultFields,
            IsCollection = true,
            IsPaged = true,
            EntityName = entity.Name,
        };

        return (
            new List<CommandModel> { createCommand, updateCommand, deleteCommand },
            new List<QueryModel> { getByIdQuery, getAllQuery });
    }

    private static List<FieldModel> CloneFields(IEnumerable<FieldModel> fields) =>
        fields.Select(f => new FieldModel { Name = f.Name, Type = f.Type, Rules = new List<string>(f.Rules) }).ToList();
}
