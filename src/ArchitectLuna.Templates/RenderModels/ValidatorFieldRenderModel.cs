namespace ArchitectLuna.Templates.RenderModels;

public sealed class ValidatorFieldRenderModel
{
    public required string Name { get; init; }

    /// <summary>
    /// FluentValidation rule expressions, each already including its own trailing parentheses
    /// and arguments (e.g. "GreaterThan(0)"). The validator template appends each entry directly
    /// after a leading '.', so it must not add its own parens.
    /// </summary>
    public required IReadOnlyList<string> Rules { get; init; }
}
