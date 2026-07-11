using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ArchitectLuna.Core.ProtectedRegions;

/// <summary>
/// Preserves hand-edited content inside "// &lt;architect:region name="..."&gt; ... // &lt;/architect:region&gt;"
/// markers across regeneration. Everything outside a marker is always fully owned by the
/// generator and is overwritten on every run.
/// </summary>
public static partial class ProtectedRegionMerger
{
    public static string Merge(string generatedContent, string? existingContent)
    {
        if (existingContent is null)
        {
            return generatedContent;
        }

        var existingRegions = ExtractRegions(existingContent);
        if (existingRegions.Count == 0)
        {
            return generatedContent;
        }

        return RegionPattern().Replace(generatedContent, match =>
        {
            var name = match.Groups["name"].Value;
            if (!existingRegions.TryGetValue(name, out var existingBody))
            {
                return match.Value;
            }

            return $"// <architect:region name=\"{name}\">{existingBody}// </architect:region>";
        });
    }

    /// <summary>
    /// Result of <see cref="MergeTracked"/>: the merged file content, plus the region-name→hash
    /// map to persist (in <c>.architect/manifest.json</c>) for next time.
    /// </summary>
    public readonly record struct TrackedMergeResult(string Content, IReadOnlyDictionary<string, string> RegionHashes);

    /// <summary>
    /// Like <see cref="Merge"/>, but distinguishes a region that's still exactly what the tool
    /// itself last wrote there ("pristine") from one a human has since hand-edited. A pristine
    /// region is safe to refresh with the freshly rendered content — this is what lets `add
    /// field`/`sync entity` (docs/requirements/003-improvements.md §2.1, §7) actually update a
    /// persistence-generated handler body to reference a newly added field, instead of the plain
    /// <see cref="Merge"/> behavior of preserving *any* existing region content unconditionally
    /// (which would silently leave the old body in place, forever out of sync with the model, the
    /// moment a handler had ever been generated once).
    ///
    /// A region is trusted "pristine" only when the hash of its current on-disk content matches
    /// the hash recorded the last time this method wrote it — i.e. nothing has touched it since.
    /// The moment a region's on-disk content diverges from its last known hash, it's treated as
    /// hand-edited and preserved exactly like <see cref="Merge"/> — and, critically, its recorded
    /// hash is *not* updated in that case, so it stays flagged as hand-edited (and therefore
    /// preserved) on every future run, even if this exact edit is still on disk unchanged next
    /// time. A region with no recorded hash yet (a file written before this tracking existed, or
    /// by <see cref="Merge"/>) is preserved this one time and starts being tracked from here.
    /// </summary>
    public static TrackedMergeResult MergeTracked(string generatedContent, string? existingContent, IReadOnlyDictionary<string, string> knownRegionHashes)
    {
        if (existingContent is null)
        {
            return new TrackedMergeResult(generatedContent, PristineHashesOf(generatedContent));
        }

        var existingRegions = ExtractRegions(existingContent);
        if (existingRegions.Count == 0)
        {
            return new TrackedMergeResult(generatedContent, PristineHashesOf(generatedContent));
        }

        var updatedHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        var merged = RegionPattern().Replace(generatedContent, match =>
        {
            var name = match.Groups["name"].Value;
            var freshBody = match.Groups["body"].Value;

            if (!existingRegions.TryGetValue(name, out var existingBody))
            {
                // No prior region under this name (e.g. it's new) — the fresh render is pristine.
                updatedHashes[name] = Hash(freshBody);
                return match.Value;
            }

            var isPristine = knownRegionHashes.TryGetValue(name, out var knownHash) && knownHash == Hash(existingBody);
            if (isPristine)
            {
                updatedHashes[name] = Hash(freshBody);
                return match.Value;
            }

            updatedHashes[name] = knownRegionHashes.TryGetValue(name, out var stillDirty) ? stillDirty : Hash(existingBody);
            return $"// <architect:region name=\"{name}\">{existingBody}// </architect:region>";
        });

        return new TrackedMergeResult(merged, updatedHashes);
    }

    private static Dictionary<string, string> PristineHashesOf(string content) =>
        ExtractRegions(content).ToDictionary(r => r.Key, r => Hash(r.Value), StringComparer.Ordinal);

    private static Dictionary<string, string> ExtractRegions(string content)
    {
        var regions = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in RegionPattern().Matches(content))
        {
            regions[match.Groups["name"].Value] = match.Groups["body"].Value;
        }

        return regions;
    }

    private static string Hash(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    [GeneratedRegex(@"// <architect:region name=""(?<name>[^""]+)"">(?<body>.*?)// </architect:region>", RegexOptions.Singleline)]
    private static partial Regex RegionPattern();
}
