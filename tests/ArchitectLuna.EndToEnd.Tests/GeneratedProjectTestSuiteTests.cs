using ArchitectLuna.EndToEnd.Tests.Infrastructure;
using Xunit;

namespace ArchitectLuna.EndToEnd.Tests;

/// <summary>
/// docs/requirements/002-testing-layer.md §CI Smoke Matrix: "for at least one representative
/// combination, CI should also run `dotnet test` on the generated project." Building proves the
/// generated code compiles; running its scaffolded test suite proves the generated app actually
/// *starts* — this is exactly the tier that caught WolverineFx's runtime-compilation split
/// (generated apps compiled fine but threw at startup). The representative combination is
/// Clean Architecture + wolverine + in-memory: the layout with the most projects, the adapter
/// with the startup-time codegen risk, and a persistence provider that needs no external
/// database, so the generated health-check test can boot the app for real.
/// </summary>
public sealed class GeneratedProjectTestSuiteTests
{
    private const string SolutionName = "TestSuiteApi";

    [Fact]
    [Trait("Category", "EndToEnd")]
    public void GeneratedSolution_OwnTestSuitePasses()
    {
        var cliDllPath = CliLocator.ResolveCliDllPath();
        var workDir = TempWorkspace.CreateUnique();
        try
        {
            var newApi = ProcessRunner.RunCli(cliDllPath, workDir, "new", "api", SolutionName, "--adapter", "wolverine", "--persistence", "in-memory", "--architecture", "clean-architecture");
            Assert.True(newApi.ExitCode == 0, $"'new api' failed:\n{newApi}");

            var solutionRoot = Path.Combine(workDir, SolutionName);

            var addFeature = ProcessRunner.RunCli(cliDllPath, solutionRoot, "add", "feature", "Widgets");
            Assert.True(addFeature.ExitCode == 0, $"'add feature' failed:\n{addFeature}");

            var addEntity = ProcessRunner.RunCli(cliDllPath, solutionRoot, "add", "entity", "Widgets", "Widget", "--field", "Name:string", "--rule", "Name:NotEmpty()");
            Assert.True(addEntity.ExitCode == 0, $"'add entity' failed:\n{addEntity}");

            var generate = ProcessRunner.RunCli(cliDllPath, solutionRoot, "generate");
            Assert.True(generate.ExitCode == 0, $"'generate' failed:\n{generate}");

            var build = ProcessRunner.RunDotnetBuild(solutionRoot, TimeSpan.FromMinutes(5));
            Assert.True(build.ExitCode == 0, $"'dotnet build' failed:\n{build}");

            var test = ProcessRunner.Run("dotnet", new[] { "test", "--no-build" }, solutionRoot, TimeSpan.FromMinutes(5));
            Assert.True(test.ExitCode == 0, $"the generated solution's own test suite failed:\n{test}");
        }
        finally
        {
            TempWorkspace.TryDelete(workDir);
        }
    }
}
