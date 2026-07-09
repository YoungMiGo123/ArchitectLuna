namespace ArchitectLuna.Core.Model;

/// <summary>
/// Root of the Intent Model persisted at .architect/model.yaml.
/// </summary>
public sealed class ArchitectModel
{
    public int SchemaVersion { get; init; } = 1;

    public required string SolutionName { get; init; }

    public required string Namespace { get; init; }

    /// <summary>
    /// The backend adapter that owns code generation for this model: "mediatr" or "wolverine".
    /// </summary>
    public required string Adapter { get; init; }

    /// <summary>
    /// Persistence backend for generated CRUD handlers. Defaults to None, which preserves the
    /// original placeholder-body behavior.
    /// </summary>
    public PersistenceProvider Persistence { get; init; } = PersistenceProvider.None;

    /// <summary>
    /// Output shape. Defaults to VerticalSlice, preserving the original single-project behavior.
    /// </summary>
    public SolutionLayout Layout { get; init; } = SolutionLayout.VerticalSlice;

    public List<FeatureModel> Features { get; init; } = new();
}
