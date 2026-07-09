namespace ArchitectLuna.Templates.RenderModels;

/// <summary>
/// A plain positional record — used by the shared Record.cs.sbn template for the three DTO-ish
/// files every slice can carry: {Op}Result (Application), {Op}Request and {Op}Response
/// (Contracts target; slice-local under vertical slice).
/// </summary>
public sealed class RecordRenderModel
{
    public required string Namespace { get; init; }

    public required string RecordName { get; init; }

    public required IReadOnlyList<MessageFieldRenderModel> Fields { get; init; }
}
