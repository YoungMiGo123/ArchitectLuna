namespace ArchitectLuna.Core.Validation;

public sealed record ValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public static readonly ValidationResult Success = new(Array.Empty<string>());
}
