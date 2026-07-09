namespace ArchitectLuna.Core.Model;

public sealed class CommandModel
{
    public required string Name { get; init; }

    public CommandKind Kind { get; init; } = CommandKind.Create;

    public List<FieldModel> Fields { get; init; } = new();

    /// <summary>
    /// Explicit route override. Null means the adapter infers a default route from
    /// <see cref="Kind"/> (POST /api/{feature} for Create, PUT/DELETE /api/{feature}/{id} for
    /// Update/Delete).
    /// </summary>
    public string? Route { get; init; }

    /// <summary>
    /// Set by <see cref="CrudSynthesizer"/> to the owning entity's name; null for a hand-authored
    /// command. A configured <c>IPersistenceGenerator</c> uses this to know which DbSet/document
    /// type a handler should operate on — a command with no entity link always gets the
    /// placeholder NotImplementedException body, persistence or not.
    /// </summary>
    public string? EntityName { get; init; }
}
