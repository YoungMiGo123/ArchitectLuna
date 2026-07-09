using ArchitectLuna.EndToEnd.Tests.Infrastructure;
using Xunit;

namespace ArchitectLuna.EndToEnd.Tests;

/// <summary>
/// Full-pipeline regression coverage: for every adapter x persistence combination, actually
/// scaffold a solution, add a feature/entity, generate code, and run a real `dotnet build` over
/// the result. This is the only thing that would have caught (and now guards against regressing)
/// three real bugs that slipped past the fast unit-test suite: a Scriban double-paren validator
/// bug, an MSBuild satellite-resource culture-inference bug that silently dropped all embedded
/// templates, and a missing CancellationToken parameter on Wolverine handlers.
///
/// Slow and network-dependent (NuGet restores, several `dotnet` process launches, real MSBuild
/// compiles per case) — tagged with the "EndToEnd" Category trait so it can be run/filtered
/// separately from the fast ArchitectLuna.Core.Tests tier, e.g.:
///   dotnet test --filter Category=EndToEnd
///   dotnet test --filter Category!=EndToEnd
/// </summary>
public sealed class GeneratedSolutionBuildTests
{
    private const string SolutionName = "TestApi";
    private const string FeatureName = "Widgets";
    private const string EntityName = "Widget";

    [Theory]
    [Trait("Category", "EndToEnd")]
    [InlineData("mediatr", "none")]
    [InlineData("mediatr", "in-memory")]
    [InlineData("mediatr", "efcore-postgres")]
    [InlineData("mediatr", "efcore-sqlserver")]
    [InlineData("mediatr", "marten")]
    [InlineData("wolverine", "none")]
    [InlineData("wolverine", "in-memory")]
    [InlineData("wolverine", "efcore-postgres")]
    [InlineData("wolverine", "efcore-sqlserver")]
    [InlineData("wolverine", "marten")]
    public void GeneratedSolution_Compiles_And_MatchesKnownGoodRegressions(string adapter, string persistence)
    {
        var cliDllPath = CliLocator.ResolveCliDllPath();
        var workDir = TempWorkspace.CreateUnique();
        try
        {
            var newApi = ProcessRunner.RunCli(cliDllPath, workDir, "new", "api", SolutionName, "--adapter", adapter, "--persistence", persistence);
            Assert.True(newApi.ExitCode == 0, $"'new api' (adapter={adapter}, persistence={persistence}) failed:\n{newApi}");

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

            var generate = ProcessRunner.RunCli(cliDllPath, solutionRoot, "generate");
            Assert.True(generate.ExitCode == 0, $"'generate' failed:\n{generate}");

            AssertKnownGoodRegressions(solutionRoot);

            var build = ProcessRunner.RunDotnetBuild(solutionRoot, TimeSpan.FromMinutes(5));
            Assert.True(build.ExitCode == 0, $"'dotnet build' of the generated solution (adapter={adapter}, persistence={persistence}) failed:\n{build}");
        }
        finally
        {
            TempWorkspace.TryDelete(workDir);
        }
    }

    /// <summary>
    /// Content-level checks for specific known-good regressions, independent of whether the
    /// solution happens to compile (a subtle wording/route bug can compile fine and still be
    /// wrong at runtime).
    /// </summary>
    private static void AssertKnownGoodRegressions(string solutionRoot)
    {
        var apiProjectDir = Path.Combine(solutionRoot, "src", $"{SolutionName}.Api");
        var widgetsDir = Path.Combine(apiProjectDir, "Features", FeatureName);

        // Regression: a Scriban double-paren bug used to render validator rule lines as
        // ".GreaterThan(0)();" instead of ".GreaterThan(0);".
        foreach (var validatorFile in new[] { "CreateWidget/CreateWidgetValidator.cs", "UpdateWidget/UpdateWidgetValidator.cs" })
        {
            var path = Path.Combine(widgetsDir, validatorFile.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path), $"Expected generated validator at {path}");

            var content = File.ReadAllText(path);
            Assert.Contains(".GreaterThan(0);", content);
            Assert.DoesNotContain(".GreaterThan(0)();", content);
        }

        // Regression: GetById routes must use the inferred "/{id}" shape, not a kebab-cased
        // fallback derived from the query name.
        var endpointPath = Path.Combine(widgetsDir, "GetWidgetById", "GetWidgetByIdEndpoint.cs");
        Assert.True(File.Exists(endpointPath), $"Expected generated endpoint at {endpointPath}");

        var endpointContent = File.ReadAllText(endpointPath);
        Assert.Contains("/api/widgets/{id}", endpointContent);
        Assert.DoesNotContain("/api/widgets/get-widget-by-id", endpointContent);
    }
}
