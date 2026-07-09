namespace ArchitectLuna.Core.Model;

/// <summary>
/// A single property on a command's payload.
/// </summary>
public sealed class FieldModel
{
    public required string Name { get; init; }

    public required string Type { get; init; }

    /// <summary>
    /// FluentValidation rule expressions applied to this field. Each entry is spliced directly
    /// after a leading '.' in the generated validator (e.g. "RuleFor(x => x.Field)" + "." + rule),
    /// so every entry must include its own trailing parentheses and arguments, e.g.
    /// "GreaterThan(0)", "MaximumLength(3)", "NotEmpty()". Do NOT omit the parens here and rely on
    /// the template to add them — that produces invalid double-call syntax like "GreaterThan(0)()".
    /// </summary>
    public List<string> Rules { get; init; } = new();
}
