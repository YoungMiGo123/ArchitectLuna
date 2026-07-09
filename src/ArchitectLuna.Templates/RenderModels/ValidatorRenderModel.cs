namespace ArchitectLuna.Templates.RenderModels;

public sealed class ValidatorRenderModel
{
    public required string Namespace { get; init; }

    public required string MessageName { get; init; }

    public required string ValidatorName { get; init; }

    public required IReadOnlyList<ValidatorFieldRenderModel> Fields { get; init; }
}
