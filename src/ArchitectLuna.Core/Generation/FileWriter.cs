using ArchitectLuna.Core.Manifest;
using ArchitectLuna.Core.ProtectedRegions;

namespace ArchitectLuna.Core.Generation;

/// <summary>
/// Writes a generated file to disk, preserving any protected-region content already present,
/// and records the path in the manifest.
/// </summary>
public static class FileWriter
{
    public static void Write(string rootDirectory, GeneratedFile file, GenerationManifest manifest)
    {
        var fullPath = Path.Combine(rootDirectory, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var existingContent = File.Exists(fullPath) ? File.ReadAllText(fullPath) : null;
        var merged = ProtectedRegionMerger.Merge(file.Content, existingContent);
        File.WriteAllText(fullPath, merged);

        if (!manifest.GeneratedFiles.Contains(file.RelativePath, StringComparer.Ordinal))
        {
            manifest.GeneratedFiles.Add(file.RelativePath);
        }
    }
}
