namespace ArchitectLuna.Core.Manifest;

/// <summary>
/// Tracks every file path the tool has ever generated, so future tooling (e.g. cleanup of
/// removed features) can distinguish generated output from hand-authored files.
/// </summary>
public sealed class GenerationManifest
{
    public int SchemaVersion { get; init; } = 1;

    public List<string> GeneratedFiles { get; init; } = new();

    /// <summary>
    /// Content hash of each protected region as last written by the tool, keyed by
    /// "{relativePath}::{regionName}" — lets <see cref="ArchitectLuna.Core.ProtectedRegions.ProtectedRegionMerger.MergeTracked"/>
    /// tell an untouched ("pristine") region apart from one a human has hand-edited, so
    /// regeneration can safely refresh the former (e.g. a handler body picking up a field added
    /// via `add field`) while still preserving the latter forever. See that method's doc comment
    /// for the full contract.
    /// </summary>
    public Dictionary<string, string> RegionHashes { get; init; } = new();
}
