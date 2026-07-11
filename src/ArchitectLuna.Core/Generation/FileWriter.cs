using ArchitectLuna.Core.Manifest;
using ArchitectLuna.Core.ProtectedRegions;

namespace ArchitectLuna.Core.Generation;

/// <summary>
/// Writes a generated file to disk, preserving any protected-region content already present,
/// and records the path in the manifest.
/// </summary>
public static class FileWriter
{
    private const string RegionKeySeparator = "::";

    public static void Write(string rootDirectory, GeneratedFile file, GenerationManifest manifest)
    {
        var fullPath = Path.Combine(rootDirectory, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var existingContent = File.Exists(fullPath) ? File.ReadAllText(fullPath) : null;

        // Scope the known-hash lookup to this file's own regions (keys are
        // "{relativePath}::{regionName}" — see ProtectedRegionMerger.MergeTracked's doc comment).
        var keyPrefix = file.RelativePath + RegionKeySeparator;
        var knownHashes = manifest.RegionHashes
            .Where(kv => kv.Key.StartsWith(keyPrefix, StringComparison.Ordinal))
            .ToDictionary(kv => kv.Key[keyPrefix.Length..], kv => kv.Value, StringComparer.Ordinal);

        var result = ProtectedRegionMerger.MergeTracked(file.Content, existingContent, knownHashes);
        File.WriteAllText(fullPath, result.Content);

        foreach (var (regionName, hash) in result.RegionHashes)
        {
            manifest.RegionHashes[keyPrefix + regionName] = hash;
        }

        if (!manifest.GeneratedFiles.Contains(file.RelativePath, StringComparer.Ordinal))
        {
            manifest.GeneratedFiles.Add(file.RelativePath);
        }
    }
}
