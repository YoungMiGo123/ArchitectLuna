using ArchitectLuna.Core.Manifest;
using Xunit;

namespace ArchitectLuna.Core.Tests.Manifest;

public sealed class ManifestStoreTests
{
    [Fact]
    public void Load_MissingFile_ReturnsEmptyManifest()
    {
        var path = Path.Combine(Path.GetTempPath(), $"architect-luna-{Guid.NewGuid():N}.json");

        var manifest = ManifestStore.Load(path);

        Assert.Equal(1, manifest.SchemaVersion);
        Assert.Empty(manifest.GeneratedFiles);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsGeneratedFiles()
    {
        var path = Path.Combine(Path.GetTempPath(), $"architect-luna-{Guid.NewGuid():N}.json");
        try
        {
            var manifest = new GenerationManifest
            {
                GeneratedFiles = { "src/Api/Features/Invoices/CreateInvoice/CreateInvoiceCommand.cs" },
            };

            ManifestStore.Save(path, manifest);
            var loaded = ManifestStore.Load(path);

            Assert.Equal(manifest.GeneratedFiles, loaded.GeneratedFiles);
            Assert.Equal(manifest.SchemaVersion, loaded.SchemaVersion);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Save_DeduplicatesAndSortsPaths_SoRegenerationIsIdempotent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"architect-luna-{Guid.NewGuid():N}.json");
        try
        {
            var manifest = new GenerationManifest
            {
                GeneratedFiles = { "b.cs", "a.cs", "b.cs" },
            };

            ManifestStore.Save(path, manifest);
            var loaded = ManifestStore.Load(path);

            Assert.Equal(new[] { "a.cs", "b.cs" }, loaded.GeneratedFiles);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
