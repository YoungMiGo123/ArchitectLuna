namespace ArchitectLuna.Core.Model;

/// <summary>Parses/renders the kebab-case CLI spelling of <see cref="DatabaseApplyMode"/> ("manual", "on-generate", "on-startup").</summary>
public static class DatabaseApplyModeParser
{
    public static readonly string[] KnownValues = { "manual", "on-generate", "on-startup" };

    public static DatabaseApplyMode Parse(string value) => value switch
    {
        "manual" => DatabaseApplyMode.Manual,
        "on-generate" => DatabaseApplyMode.OnGenerate,
        "on-startup" => DatabaseApplyMode.OnStartup,
        _ => throw new InvalidOperationException(
            $"Unknown database apply mode '{value}'. Valid values: {string.Join(", ", KnownValues)}."),
    };

    public static string ToKebabCase(DatabaseApplyMode mode) => mode switch
    {
        DatabaseApplyMode.Manual => "manual",
        DatabaseApplyMode.OnGenerate => "on-generate",
        DatabaseApplyMode.OnStartup => "on-startup",
        _ => throw new InvalidOperationException($"Unknown database apply mode '{mode}'."),
    };
}
