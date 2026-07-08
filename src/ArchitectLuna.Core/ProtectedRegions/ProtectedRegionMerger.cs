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

    private static Dictionary<string, string> ExtractRegions(string content)
    {
        var regions = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in RegionPattern().Matches(content))
        {
            regions[match.Groups["name"].Value] = match.Groups["body"].Value;
        }

        return regions;
    }

    [GeneratedRegex(@"// <architect:region name=""(?<name>[^""]+)"">(?<body>.*?)// </architect:region>", RegexOptions.Singleline)]
    private static partial Regex RegionPattern();
}
