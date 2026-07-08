namespace ArchitectLuna.Core.Manifest;

/// <summary>
/// Tracks every file path the tool has ever generated, so future tooling (e.g. cleanup of
/// removed features) can distinguish generated output from hand-authored files.
/// </summary>
public sealed class GenerationManifest
{
    public int SchemaVersion { get; init; } = 1;

    public List<string> GeneratedFiles { get; init; } = new();
}
