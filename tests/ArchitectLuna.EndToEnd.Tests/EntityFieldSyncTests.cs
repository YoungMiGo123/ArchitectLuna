using ArchitectLuna.EndToEnd.Tests.Infrastructure;
using Xunit;

namespace ArchitectLuna.EndToEnd.Tests;

/// <summary>
/// docs/requirements/003-improvements.md §2.1, §7: `add field` (and its `update entity
/// --add-field` alias) mutate an existing entity and regenerate every dependent artifact through
/// the real CLI, and `sync entity`/`config set database.applyMode` are wired end to end.
/// </summary>
public sealed class EntityFieldSyncTests
{
    [Fact]
    [Trait("Category", "EndToEnd")]
    public void AddField_UpdatesEntityAndEveryDependentGeneratedFile()
    {
        var cliDllPath = CliLocator.ResolveCliDllPath();
        var workDir = TempWorkspace.CreateUnique();
        try
        {
            var newApi = ProcessRunner.RunCli(cliDllPath, workDir, "new", "api", "FieldSyncApi", "--adapter", "mediatr", "--persistence", "in-memory", "--architecture", "vertical-slice", "--no-format");
            Assert.True(newApi.ExitCode == 0, $"'new api' failed:\n{newApi}");
            var solutionRoot = Path.Combine(workDir, "FieldSyncApi");

            Assert.True(ProcessRunner.RunCli(cliDllPath, solutionRoot, "add", "feature", "Invoices").ExitCode == 0);
            Assert.True(ProcessRunner.RunCli(cliDllPath, solutionRoot, "add", "entity", "Invoices", "Invoice", "--field", "AmountCents:long").ExitCode == 0);
            Assert.True(ProcessRunner.RunCli(cliDllPath, solutionRoot, "generate", "--no-format").ExitCode == 0);

            var addField = ProcessRunner.RunCli(cliDllPath, solutionRoot, "add", "field", "Invoices", "Invoice", "Reference:string");
            Assert.True(addField.ExitCode == 0, $"'add field' failed:\n{addField}");

            var slice = Path.Combine(solutionRoot, "src", "FieldSyncApi.Api", "Features", "Invoices");
            var createCommand = File.ReadAllText(Path.Combine(slice, "CreateInvoice", "CreateInvoiceCommand.cs"));
            Assert.Contains("Reference", createCommand);

            var updateCommand = File.ReadAllText(Path.Combine(slice, "UpdateInvoice", "UpdateInvoiceCommand.cs"));
            Assert.Contains("Reference", updateCommand);

            var getByIdResult = File.ReadAllText(Path.Combine(slice, "GetInvoiceById", "GetInvoiceByIdResult.cs"));
            Assert.Contains("Reference", getByIdResult);

            var validator = File.ReadAllText(Path.Combine(slice, "CreateInvoice", "CreateInvoiceValidator.cs"));
            Assert.Contains("Reference", validator);

            var build = ProcessRunner.RunDotnetBuild(solutionRoot);
            Assert.True(build.ExitCode == 0, $"build failed after 'add field':\n{build}");
        }
        finally
        {
            TempWorkspace.TryDelete(workDir);
        }
    }

    [Fact]
    [Trait("Category", "EndToEnd")]
    public void AddField_UnknownEntity_FailsWithGuidance()
    {
        var cliDllPath = CliLocator.ResolveCliDllPath();
        var workDir = TempWorkspace.CreateUnique();
        try
        {
            var newApi = ProcessRunner.RunCli(cliDllPath, workDir, "new", "api", "MissingEntityApi", "--adapter", "mediatr", "--persistence", "none", "--architecture", "vertical-slice", "--no-format");
            Assert.True(newApi.ExitCode == 0, $"'new api' failed:\n{newApi}");
            var solutionRoot = Path.Combine(workDir, "MissingEntityApi");

            Assert.True(ProcessRunner.RunCli(cliDllPath, solutionRoot, "add", "feature", "Invoices").ExitCode == 0);

            var addField = ProcessRunner.RunCli(cliDllPath, solutionRoot, "add", "field", "Invoices", "Invoice", "Reference:string");
            Assert.NotEqual(0, addField.ExitCode);
            Assert.Contains("Create the entity first", addField.CombinedOutputNormalized());
        }
        finally
        {
            TempWorkspace.TryDelete(workDir);
        }
    }

    [Fact]
    [Trait("Category", "EndToEnd")]
    public void UpdateEntity_AddFieldAlias_BehavesLikeAddField()
    {
        var cliDllPath = CliLocator.ResolveCliDllPath();
        var workDir = TempWorkspace.CreateUnique();
        try
        {
            var newApi = ProcessRunner.RunCli(cliDllPath, workDir, "new", "api", "UpdateEntityApi", "--adapter", "mediatr", "--persistence", "none", "--architecture", "vertical-slice", "--no-format");
            Assert.True(newApi.ExitCode == 0, $"'new api' failed:\n{newApi}");
            var solutionRoot = Path.Combine(workDir, "UpdateEntityApi");

            Assert.True(ProcessRunner.RunCli(cliDllPath, solutionRoot, "add", "feature", "Invoices").ExitCode == 0);
            Assert.True(ProcessRunner.RunCli(cliDllPath, solutionRoot, "add", "entity", "Invoices", "Invoice", "--field", "AmountCents:long").ExitCode == 0);

            var update = ProcessRunner.RunCli(cliDllPath, solutionRoot, "update", "entity", "Invoices", "Invoice", "--add-field", "Reference:string", "--no-format");
            Assert.True(update.ExitCode == 0, $"'update entity --add-field' failed:\n{update}");

            var modelYaml = File.ReadAllText(Path.Combine(solutionRoot, ".architect", "model.yaml"));
            Assert.Contains("Reference", modelYaml);
        }
        finally
        {
            TempWorkspace.TryDelete(workDir);
        }
    }

    [Fact]
    [Trait("Category", "EndToEnd")]
    public void ConfigSet_DatabaseApplyMode_UpdatesModel()
    {
        var cliDllPath = CliLocator.ResolveCliDllPath();
        var workDir = TempWorkspace.CreateUnique();
        try
        {
            var newApi = ProcessRunner.RunCli(cliDllPath, workDir, "new", "api", "ConfigSetApi", "--adapter", "mediatr", "--persistence", "none", "--architecture", "vertical-slice", "--no-format");
            Assert.True(newApi.ExitCode == 0, $"'new api' failed:\n{newApi}");
            var solutionRoot = Path.Combine(workDir, "ConfigSetApi");

            var configSet = ProcessRunner.RunCli(cliDllPath, solutionRoot, "config", "set", "database.applyMode", "on-startup");
            Assert.True(configSet.ExitCode == 0, $"'config set' failed:\n{configSet}");

            var modelYaml = File.ReadAllText(Path.Combine(solutionRoot, ".architect", "model.yaml"));
            Assert.Contains("applyMode", modelYaml, StringComparison.OrdinalIgnoreCase);

            var badValue = ProcessRunner.RunCli(cliDllPath, solutionRoot, "config", "set", "database.applyMode", "sometimes");
            Assert.NotEqual(0, badValue.ExitCode);
        }
        finally
        {
            TempWorkspace.TryDelete(workDir);
        }
    }

    [Fact]
    [Trait("Category", "EndToEnd")]
    public void SyncEntity_UnknownEntity_FailsClearly()
    {
        var cliDllPath = CliLocator.ResolveCliDllPath();
        var workDir = TempWorkspace.CreateUnique();
        try
        {
            var newApi = ProcessRunner.RunCli(cliDllPath, workDir, "new", "api", "SyncApi", "--adapter", "mediatr", "--persistence", "none", "--architecture", "vertical-slice", "--no-format");
            Assert.True(newApi.ExitCode == 0, $"'new api' failed:\n{newApi}");
            var solutionRoot = Path.Combine(workDir, "SyncApi");

            Assert.True(ProcessRunner.RunCli(cliDllPath, solutionRoot, "add", "feature", "Invoices").ExitCode == 0);

            var sync = ProcessRunner.RunCli(cliDllPath, solutionRoot, "sync", "entity", "Invoices", "Invoice");
            Assert.NotEqual(0, sync.ExitCode);
            Assert.Contains("does not exist", sync.CombinedOutputNormalized());
        }
        finally
        {
            TempWorkspace.TryDelete(workDir);
        }
    }
}
