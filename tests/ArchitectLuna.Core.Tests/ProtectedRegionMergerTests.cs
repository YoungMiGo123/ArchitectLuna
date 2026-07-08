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
}
