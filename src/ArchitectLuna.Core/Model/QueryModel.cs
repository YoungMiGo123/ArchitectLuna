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
    /// True for a CRUD-synthesized GetAll query: the result is wrapped as
    /// PagedResult&lt;TResult&gt; instead of a raw list, and the generated handler pages
    /// (Skip/Take or provider equivalent) instead of loading every row. Page/PageSize never join
    /// <see cref="Params"/> — they're query-string-only and must not affect route inference.
    /// </summary>
    public bool IsPaged { get; init; }

    /// <summary>
    /// Explicit route override. Null means the adapter infers a default route.
    /// </summary>
    public string? Route { get; init; }

    /// <summary>
    /// Set by <see cref="CrudSynthesizer"/> to the owning entity's name; null for a hand-authored
    /// query. A configured <c>IPersistenceGenerator</c> uses this to know which DbSet/document
    /// type a handler should read from — a query with no entity link always gets the placeholder
    /// NotImplementedException body, persistence or not.
    /// </summary>
    public string? EntityName { get; init; }
}
