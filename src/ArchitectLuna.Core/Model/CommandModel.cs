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
}
