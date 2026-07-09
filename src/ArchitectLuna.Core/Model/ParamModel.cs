namespace ArchitectLuna.Core.Model;

/// <summary>
/// A single query parameter.
/// </summary>
public sealed class ParamModel
{
    public required string Name { get; init; }

    public required string Type { get; init; }
}
