namespace ArchitectLuna.Core.Editing;

/// <summary>
/// Outcome of a <see cref="ModelEditor"/> mutation. <see cref="AddedOperations"/> lists the
/// command/query names a successful edit synthesized (entity CRUD), so callers (CLI, UI) can
/// report exactly what was added without re-deriving it.
/// </summary>
public sealed record EditResult(bool Success, string? Error, IReadOnlyList<string> AddedOperations)
{
    public static EditResult Ok(IReadOnlyList<string>? addedOperations = null) =>
        new(true, null, addedOperations ?? Array.Empty<string>());

    public static EditResult Fail(string error) => new(false, error, Array.Empty<string>());
}
