using ArchitectLuna.EndToEnd.Tests.Infrastructure;
using Xunit;

namespace ArchitectLuna.EndToEnd.Tests;

/// <summary>
/// Verifies the protected-region contract end to end: hand-written content inside an
/// "// &lt;architect:region name="..."&gt; ... // &lt;/architect:region&gt;" marker survives a
/// re-run of `generate`, while everything outside the marker is fully re-rendered ("regenerated
/// normally") on every run — proving the merge is selective, not "skip files that already exist."
///
/// Tagged "EndToEnd" like <see cref="GeneratedSolutionBuildTests"/> — shells out to the real CLI
/// and runs `generate` twice against a scaffolded solution.
/// </summary>
public sealed class ProtectedRegionRegenerationTests
{
    private const string SolutionName = "TestApi";
    private const string FeatureName = "Widgets";
    private const string EntityName = "Widget";
    private const string RegionOpenTag = "// <architect:region name=\"handler-body\">";
    private const string InsideRegionMarker = "// E2E_PROTECTED_REGION_MARKER_4f3c9d";
    private const string OutsideRegionMarker = "// E2E_SHOULD_BE_OVERWRITTEN_9a1b2c";

    [Fact]
    [Trait("Category", "EndToEnd")]
    public void Regenerate_PreservesHandEditedProtectedRegion_ButOverwritesEverythingElse()
    {
        var cliDllPath = CliLocator.ResolveCliDllPath();
        var workDir = TempWorkspace.CreateUnique();
        try
        {
            var newApi = ProcessRunner.RunCli(cliDllPath, workDir, "new", "api", SolutionName, "--adapter", "mediatr", "--persistence", "none");
            Assert.True(newApi.ExitCode == 0, $"'new api' failed:\n{newApi}");

            var solutionRoot = Path.Combine(workDir, SolutionName);

            var addFeature = ProcessRunner.RunCli(cliDllPath, solutionRoot, "add", "feature", FeatureName);
            Assert.True(addFeature.ExitCode == 0, $"'add feature' failed:\n{addFeature}");

            var addEntity = ProcessRunner.RunCli(
                cliDllPath,
                solutionRoot,
                "add", "entity", FeatureName, EntityName,
                "--field", "Name:string",
                "--field", "Quantity:int",
                "--rule", "Quantity:GreaterThan(0)");
            Assert.True(addEntity.ExitCode == 0, $"'add entity' failed:\n{addEntity}");

            var firstGenerate = ProcessRunner.RunCli(cliDllPath, solutionRoot, "generate");
            Assert.True(firstGenerate.ExitCode == 0, $"'generate' failed:\n{firstGenerate}");

            var handlerPath = Path.Combine(solutionRoot, "src", $"{SolutionName}.Api", "Features", FeatureName, "CreateWidget", "CreateWidgetHandler.cs");
            Assert.True(File.Exists(handlerPath), $"Expected generated handler at {handlerPath}");

            var original = File.ReadAllText(handlerPath);
            Assert.Contains(RegionOpenTag, original);
            Assert.DoesNotContain(InsideRegionMarker, original);

            // Hand-edit: one marker inside the protected region (must survive regeneration), one
            // marker outside it (must be gone after regeneration — proves the merge is selective).
            var handEdited = OutsideRegionMarker + "\n" +
                original.Replace(RegionOpenTag, RegionOpenTag + "\n        " + InsideRegionMarker);
            Assert.NotEqual(original, handEdited);
            File.WriteAllText(handlerPath, handEdited);

            var secondGenerate = ProcessRunner.RunCli(cliDllPath, solutionRoot, "generate");
            Assert.True(secondGenerate.ExitCode == 0, $"'generate' (regeneration) failed:\n{secondGenerate}");

            var regenerated = File.ReadAllText(handlerPath);
            Assert.Contains(InsideRegionMarker, regenerated);
            Assert.DoesNotContain(OutsideRegionMarker, regenerated);
        }
        finally
        {
            TempWorkspace.TryDelete(workDir);
        }
    }
}
