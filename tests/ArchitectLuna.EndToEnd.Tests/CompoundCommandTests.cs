using ArchitectLuna.EndToEnd.Tests.Infrastructure;
using Xunit;

namespace ArchitectLuna.EndToEnd.Tests;

/// <summary>
/// docs/requirements/003-improvements.md §8, §8.1: `add entity`/`add crud` offer to create a
/// missing feature instead of failing outright. `ProcessRunner` always redirects the child's
/// stdout, so a CLI process it launches is never "interactive" by <see cref="Console.IsOutputRedirected"/>
/// — these tests exercise the two paths that are reachable without a real TTY: `--yes`/
/// `--create-missing` non-interactive success, and the no-flag non-interactive failure message.
/// The interactive Y/n prompt itself is exercised by hand (documented in the requirement), not by
/// this automated suite.
/// </summary>
public sealed class CompoundCommandTests
{
    [Fact]
    [Trait("Category", "EndToEnd")]
    public void AddEntity_MissingFeature_NonInteractive_FailsWithClearGuidance()
    {
        var cliDllPath = CliLocator.ResolveCliDllPath();
        var workDir = TempWorkspace.CreateUnique();
        try
        {
            var newApi = ProcessRunner.RunCli(cliDllPath, workDir, "new", "api", "CompoundApi", "--adapter", "mediatr", "--persistence", "none", "--architecture", "vertical-slice", "--no-format");
            Assert.True(newApi.ExitCode == 0, $"'new api' failed:\n{newApi}");
            var solutionRoot = Path.Combine(workDir, "CompoundApi");

            var addEntity = ProcessRunner.RunCli(cliDllPath, solutionRoot, "add", "entity", "Payments", "PaymentRequest", "--field", "AmountCents:long");
            Assert.NotEqual(0, addEntity.ExitCode);
            Assert.Contains("--yes", addEntity.StandardOutput + addEntity.StandardError);

            var modelYaml = File.ReadAllText(Path.Combine(solutionRoot, ".architect", "model.yaml"));
            Assert.DoesNotContain("Payments", modelYaml);
        }
        finally
        {
            TempWorkspace.TryDelete(workDir);
        }
    }

    [Fact]
    [Trait("Category", "EndToEnd")]
    public void AddEntity_MissingFeature_WithYesFlag_CreatesFeatureAndEntity()
    {
        var cliDllPath = CliLocator.ResolveCliDllPath();
        var workDir = TempWorkspace.CreateUnique();
        try
        {
            var newApi = ProcessRunner.RunCli(cliDllPath, workDir, "new", "api", "CompoundYesApi", "--adapter", "mediatr", "--persistence", "none", "--architecture", "vertical-slice", "--no-format");
            Assert.True(newApi.ExitCode == 0, $"'new api' failed:\n{newApi}");
            var solutionRoot = Path.Combine(workDir, "CompoundYesApi");

            var addEntity = ProcessRunner.RunCli(cliDllPath, solutionRoot, "add", "entity", "Payments", "PaymentRequest", "--field", "AmountCents:long", "--yes");
            Assert.True(addEntity.ExitCode == 0, $"'add entity --yes' failed:\n{addEntity}");
            Assert.Contains("Created feature 'Payments'", addEntity.StandardOutput);

            var modelYaml = File.ReadAllText(Path.Combine(solutionRoot, ".architect", "model.yaml"));
            Assert.Contains("Payments", modelYaml);
            Assert.Contains("PaymentRequest", modelYaml);
        }
        finally
        {
            TempWorkspace.TryDelete(workDir);
        }
    }

    [Fact]
    [Trait("Category", "EndToEnd")]
    public void AddCrud_MissingFeature_WithCreateMissingFlag_CreatesFeatureThenFailsOnMissingEntity()
    {
        var cliDllPath = CliLocator.ResolveCliDllPath();
        var workDir = TempWorkspace.CreateUnique();
        try
        {
            var newApi = ProcessRunner.RunCli(cliDllPath, workDir, "new", "api", "CompoundCrudApi", "--adapter", "mediatr", "--persistence", "none", "--architecture", "vertical-slice", "--no-format");
            Assert.True(newApi.ExitCode == 0, $"'new api' failed:\n{newApi}");
            var solutionRoot = Path.Combine(workDir, "CompoundCrudApi");

            // `add crud` never fabricates a meaningless entity — even with --create-missing it
            // must still ask for the entity to be created first (docs/requirements/003-improvements.md §8.2).
            var addCrud = ProcessRunner.RunCli(cliDllPath, solutionRoot, "add", "crud", "Payments", "PaymentRequest", "--create-missing");
            Assert.True(addCrud.ExitCode != 0, $"'add crud --create-missing' unexpectedly succeeded:\n{addCrud}");
            Assert.True(
                (addCrud.StandardOutput + addCrud.StandardError).Contains("Create the entity first"),
                $"Expected 'Create the entity first' guidance in output:\n{addCrud}");
        }
        finally
        {
            TempWorkspace.TryDelete(workDir);
        }
    }
}
