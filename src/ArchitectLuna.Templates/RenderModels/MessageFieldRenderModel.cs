namespace ArchitectLuna.Templates.RenderModels;

/// <summary>
/// A name+type pair used to render constructor parameters for a message or its result.
/// </summary>
public sealed class MessageFieldRenderModel
{
    public required string Name { get; init; }

    public required string Type { get; init; }
}
