using ArchitectLuna.Core.ProtectedRegions;
using Xunit;

namespace ArchitectLuna.Core.Tests;

public sealed class ProtectedRegionMergerTests
{
    [Fact]
    public void Merge_NoExistingFile_ReturnsGeneratedContentUnchanged()
    {
        const string generated = "// <architect:region name=\"handler-body\">\nthrow new NotImplementedException();\n// </architect:region>";

        var result = ProtectedRegionMerger.Merge(generated, existingContent: null);

        Assert.Equal(generated, result);
    }

    [Fact]
    public void Merge_ExistingHandEditedRegion_IsPreservedAcrossRegeneration()
    {
        const string existing =
            "public sealed class OldName\n" +
            "{\n" +
            "    // <architect:region name=\"handler-body\">\n" +
            "    var invoice = new Invoice(request.CustomerId);\n" +
            "    return new Result(invoice.Id);\n" +
            "    // </architect:region>\n" +
            "}\n";

        const string regenerated =
            "public sealed class NewName\n" +
            "{\n" +
            "    // <architect:region name=\"handler-body\">\n" +
            "    throw new NotImplementedException();\n" +
            "    // </architect:region>\n" +
            "}\n";

        var merged = ProtectedRegionMerger.Merge(regenerated, existing);

        Assert.Contains("public sealed class NewName", merged);
        Assert.Contains("var invoice = new Invoice(request.CustomerId);", merged);
        Assert.DoesNotContain("throw new NotImplementedException();", merged);
    }

    [Fact]
    public void Merge_NoMarkersInExistingFile_FullyOverwritesWithGeneratedContent()
    {
        const string existing = "public sealed class Handwritten { }";
        const string regenerated = "// <architect:region name=\"handler-body\">\ngenerated\n// </architect:region>";

        var merged = ProtectedRegionMerger.Merge(regenerated, existing);

        Assert.Equal(regenerated, merged);
    }

    private static readonly Dictionary<string, string> NoKnownHashes = new();

    [Fact]
    public void MergeTracked_FirstWrite_IsPristineAndHashesTheFreshRegion()
    {
        const string generated = "// <architect:region name=\"handler-body\">\nvar x = 1;\n// </architect:region>";

        var result = ProtectedRegionMerger.MergeTracked(generated, existingContent: null, NoKnownHashes);

        Assert.Equal(generated, result.Content);
        Assert.True(result.RegionHashes.ContainsKey("handler-body"));
    }

    [Fact]
    public void MergeTracked_PristineRegion_RefreshesToTheNewlyRenderedContent()
    {
        const string existing = "// <architect:region name=\"handler-body\">\nvar x = 1;\n// </architect:region>";
        const string regenerated = "// <architect:region name=\"handler-body\">\nvar x = 1;\nvar y = 2;\n// </architect:region>";

        // Simulate the state after the first write: the recorded hash matches what's on disk.
        var first = ProtectedRegionMerger.MergeTracked(existing, existingContent: null, NoKnownHashes);
        var second = ProtectedRegionMerger.MergeTracked(regenerated, existing, first.RegionHashes);

        Assert.Equal(regenerated, second.Content);
        Assert.Contains("var y = 2;", second.Content);
    }

    [Fact]
    public void MergeTracked_HandEditedRegion_IsPreservedForeverEvenAcrossFurtherModelChanges()
    {
        const string original = "// <architect:region name=\"handler-body\">\nvar x = 1;\n// </architect:region>";
        var afterFirstWrite = ProtectedRegionMerger.MergeTracked(original, existingContent: null, NoKnownHashes);

        // A human hand-edits the file — disk content no longer matches the recorded hash.
        const string handEdited = "// <architect:region name=\"handler-body\">\nvar x = 1; // hand-added comment\n// </architect:region>";

        // Next model change tries to regenerate the region with new content.
        const string regenerated = "// <architect:region name=\"handler-body\">\nvar x = 1;\nvar y = 2;\n// </architect:region>";
        var afterModelChange = ProtectedRegionMerger.MergeTracked(regenerated, handEdited, afterFirstWrite.RegionHashes);

        Assert.Contains("hand-added comment", afterModelChange.Content);
        Assert.DoesNotContain("var y = 2;", afterModelChange.Content);

        // A THIRD run, even with the hand edit still unchanged on disk, must not suddenly treat it
        // as pristine and discard it — the region should stay flagged dirty forever.
        const string regeneratedAgain = "// <architect:region name=\"handler-body\">\nvar x = 1;\nvar y = 2;\nvar z = 3;\n// </architect:region>";
        var afterThirdRun = ProtectedRegionMerger.MergeTracked(regeneratedAgain, handEdited, afterModelChange.RegionHashes);

        Assert.Contains("hand-added comment", afterThirdRun.Content);
        Assert.DoesNotContain("var z = 3;", afterThirdRun.Content);
    }

    [Fact]
    public void MergeTracked_UnknownRegion_IsPreservedOnceThenTrackedGoingForward()
    {
        // A file written before hash-tracking existed (e.g. by plain Merge()) has protected-region
        // content but no manifest history for it.
        const string existing = "// <architect:region name=\"handler-body\">\nvar x = 1;\n// </architect:region>";
        const string regenerated = "// <architect:region name=\"handler-body\">\nvar x = 1;\nvar y = 2;\n// </architect:region>";

        var firstRunAfterUpgrade = ProtectedRegionMerger.MergeTracked(regenerated, existing, NoKnownHashes);

        // Preserved this one time (unknown provenance — conservative default).
        Assert.DoesNotContain("var y = 2;", firstRunAfterUpgrade.Content);

        // Untouched since, so the next run can now safely refresh it.
        const string regeneratedAgain = "// <architect:region name=\"handler-body\">\nvar x = 1;\nvar y = 2;\n// </architect:region>";
        var secondRun = ProtectedRegionMerger.MergeTracked(regeneratedAgain, existing, firstRunAfterUpgrade.RegionHashes);

        Assert.Contains("var y = 2;", secondRun.Content);
    }
}
