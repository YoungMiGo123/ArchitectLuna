using ArchitectLuna.EndToEnd.Tests.Infrastructure;
using Xunit;

namespace ArchitectLuna.EndToEnd.Tests;

/// <summary>
/// docs/requirements/003-improvements.md §4: generated output is formatted automatically unless
/// `--no-format` is passed. `dotnet format` is best-effort (never fails the command), so this
/// mainly proves the flag is wired and doesn't itself break scaffolding/generation.
/// </summary>
public sealed class AutoFormattingTests
{
    [Fact]
    [Trait("Category", "EndToEnd")]
    public void NewApi_RunsFormatByDefault_AndStillBuilds()
    {
        var cliDllPath = CliLocator.ResolveCliDllPath();
        var workDir = TempWorkspace.CreateUnique();
        try
        {
            var newApi = ProcessRunner.RunCli(cliDllPath, workDir, "new", "api", "FormatDefaultApi", "--adapter", "mediatr", "--persistence", "none", "--architecture", "vertical-slice");
            Assert.True(newApi.ExitCode == 0, $"'new api' failed:\n{newApi}");

            var solutionRoot = Path.Combine(workDir, "FormatDefaultApi");
            var build = ProcessRunner.RunDotnetBuild(solutionRoot);
            Assert.True(build.ExitCode == 0, $"build failed after default formatting:\n{build}");
        }
        finally
        {
            TempWorkspace.TryDelete(workDir);
        }
    }

    [Fact]
    [Trait("Category", "EndToEnd")]
    public void NewApi_NoFormatFlag_SkipsFormattingAndStillBuilds()
    {
        var cliDllPath = CliLocator.ResolveCliDllPath();
        var workDir = TempWorkspace.CreateUnique();
        try
        {
            var newApi = ProcessRunner.RunCli(cliDllPath, workDir, "new", "api", "NoFormatApi", "--adapter", "mediatr", "--persistence", "none", "--architecture", "vertical-slice", "--no-format");
            Assert.True(newApi.ExitCode == 0, $"'new api --no-format' failed:\n{newApi}");

            var solutionRoot = Path.Combine(workDir, "NoFormatApi");
            var build = ProcessRunner.RunDotnetBuild(solutionRoot);
            Assert.True(build.ExitCode == 0, $"build failed with formatting skipped:\n{build}");
        }
        finally
        {
            TempWorkspace.TryDelete(workDir);
        }
    }

    [Fact]
    [Trait("Category", "EndToEnd")]
    public void Generate_NoFormatFlag_IsAccepted()
    {
        var cliDllPath = CliLocator.ResolveCliDllPath();
        var workDir = TempWorkspace.CreateUnique();
        try
        {
            var newApi = ProcessRunner.RunCli(cliDllPath, workDir, "new", "api", "GenerateFormatApi", "--adapter", "mediatr", "--persistence", "none", "--architecture", "vertical-slice", "--no-format");
            Assert.True(newApi.ExitCode == 0, $"'new api' failed:\n{newApi}");
            var solutionRoot = Path.Combine(workDir, "GenerateFormatApi");

            var addFeature = ProcessRunner.RunCli(cliDllPath, solutionRoot, "add", "feature", "Invoices");
            Assert.True(addFeature.ExitCode == 0, $"'add feature' failed:\n{addFeature}");

            var addEntity = ProcessRunner.RunCli(cliDllPath, solutionRoot, "add", "entity", "Invoices", "Invoice", "--field", "AmountCents:long");
            Assert.True(addEntity.ExitCode == 0, $"'add entity' failed:\n{addEntity}");

            var generate = ProcessRunner.RunCli(cliDllPath, solutionRoot, "generate", "--no-format");
            Assert.True(generate.ExitCode == 0, $"'generate --no-format' failed:\n{generate}");

            var build = ProcessRunner.RunDotnetBuild(solutionRoot);
            Assert.True(build.ExitCode == 0, $"build failed after generate --no-format:\n{build}");
        }
        finally
        {
            TempWorkspace.TryDelete(workDir);
        }
    }
}
