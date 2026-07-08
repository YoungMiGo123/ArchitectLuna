namespace ArchitectLuna.Core.Model;

public sealed class QueryModel
{
    public required string Name { get; init; }

    public List<ParamModel> Params { get; init; } = new();

    /// <summary>
    /// The shape of the query's result. Empty means "mirror Params" (the MVP default for a
    /// hand-written lookup query); an entity-backed query (see <see cref="CrudSynthesizer"/>)
    /// sets this explicitly to the entity's fields so GetById/GetAll return real data instead of
    /// echoing the lookup key back.
    /// </summary>
    public List<ParamModel> ResultFields { get; init; } = new();

    /// <summary>
    /// True for a "list" query (e.g. GetAll): the result type is wrapped as
    /// IReadOnlyList&lt;TResult&gt; instead of a single TResult.
    /// </summary>
    public bool IsCollection { get; init; }

    /// <summary>
    /// Explicit route override. Null means the adapter infers a default route.
    /// </summary>
    public string? Route { get; init; }
}
