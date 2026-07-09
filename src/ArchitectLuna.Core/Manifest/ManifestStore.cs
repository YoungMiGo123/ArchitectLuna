using System.Text.Json;

namespace ArchitectLuna.Core.Manifest;

public static class ManifestStore
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static GenerationManifest Load(string path)
    {
        if (!File.Exists(path))
        {
            return new GenerationManifest();
        }

        var json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new GenerationManifest();
        }

        return JsonSerializer.Deserialize<GenerationManifest>(json) ?? new GenerationManifest();
    }

    public static void Save(string path, GenerationManifest manifest)
    {
        var sortedFiles = manifest.GeneratedFiles
            .Distinct(StringComparer.Ordinal)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        var normalized = new GenerationManifest { SchemaVersion = manifest.SchemaVersion, GeneratedFiles = sortedFiles };
        File.WriteAllText(path, JsonSerializer.Serialize(normalized, WriteOptions));
    }
}
