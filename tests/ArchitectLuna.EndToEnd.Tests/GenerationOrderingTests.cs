using ArchitectLuna.EndToEnd.Tests.Infrastructure;
using Xunit;

namespace ArchitectLuna.EndToEnd.Tests;

/// <summary>
/// docs/requirements/002-testing-layer.md §Generation Ordering Tests, driven through the real
/// CLI: nothing can be added outside a project, entities need their feature, CRUD needs its
/// entity, duplicates fail without corrupting the model, and bespoke commands/queries are allowed
/// without an entity. The rule logic itself is unit-tested in
/// ArchitectLuna.Core.Tests/GenerationOrdering — this proves the CLI wires those rules to clear
/// error messages and non-zero exit codes.
/// </summary>
public sealed class GenerationOrderingTests
{
    [Fact]
    [Trait("Category", "EndToEnd")]
    public void AddFeature_OutsideAProject_FailsWithClearError()
    {
        var cliDllPath = CliLocator.ResolveCliDllPath();
        var workDir = TempWorkspace.CreateUnique();
        try
        {
            var result = ProcessRunner.RunCli(cliDllPath, workDir, "add", "feature", "Invoices");

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains(".architect/model.yaml", result.StandardOutput + result.StandardError);
        }
        finally
        {
            TempWorkspace.TryDelete(workDir);
        }
    }

    [Fact]
    [Trait("Category", "EndToEnd")]
    public void OrderingRules_AreEnforcedThroughTheRealCli()
    {
        var cliDllPath = CliLocator.ResolveCliDllPath();
        var workDir = TempWorkspace.CreateUnique();
        try
        {
            // One scaffold serves every rule check below — `--persistence none` keeps it fast.
            var newApi = ProcessRunner.RunCli(cliDllPath, workDir, "new", "api", "OrderingApi", "--adapter", "mediatr", "--persistence", "none", "--architecture", "vertical-slice");
            Assert.True(newApi.ExitCode == 0, $"'new api' failed:\n{newApi}");
            var solutionRoot = Path.Combine(workDir, "OrderingApi");
            var modelPath = Path.Combine(solutionRoot, ".architect", "model.yaml");

            // Rule 2: a feature must exist before entities can be added to it.
            var entityWithoutFeature = ProcessRunner.RunCli(cliDllPath, solutionRoot, "add", "entity", "Invoices", "Invoice", "--field", "AmountCents:long");
            Assert.NotEqual(0, entityWithoutFeature.ExitCode);
            Assert.Contains("add feature Invoices", entityWithoutFeature.CombinedOutputNormalized());

            var addFeature = ProcessRunner.RunCli(cliDllPath, solutionRoot, "add", "feature", "Invoices");
            Assert.True(addFeature.ExitCode == 0, $"'add feature' failed:\n{addFeature}");

            // Rule 3: CRUD requires the entity to exist first.
            var crudWithoutEntity = ProcessRunner.RunCli(cliDllPath, solutionRoot, "add", "crud", "Invoices", "Invoice");
            Assert.NotEqual(0, crudWithoutEntity.ExitCode);
            Assert.Contains("Create the entity first", crudWithoutEntity.CombinedOutputNormalized());

            var addEntity = ProcessRunner.RunCli(cliDllPath, solutionRoot, "add", "entity", "Invoices", "Invoice", "--field", "AmountCents:long");
            Assert.True(addEntity.ExitCode == 0, $"'add entity' failed:\n{addEntity}");

            // Duplicates fail safely: clear error, and the model file is untouched.
            var modelBeforeDuplicate = File.ReadAllText(modelPath);
            var duplicateEntity = ProcessRunner.RunCli(cliDllPath, solutionRoot, "add", "entity", "Invoices", "Invoice", "--field", "AmountCents:long");
            Assert.NotEqual(0, duplicateEntity.ExitCode);
            Assert.Contains("already exists", duplicateEntity.CombinedOutputNormalized());
            Assert.Equal(modelBeforeDuplicate, File.ReadAllText(modelPath));

            var duplicateFeature = ProcessRunner.RunCli(cliDllPath, solutionRoot, "add", "feature", "Invoices");
            Assert.NotEqual(0, duplicateFeature.ExitCode);
            Assert.Contains("already exists", duplicateFeature.CombinedOutputNormalized());

            // Rule 4: bespoke commands/queries are allowed without an entity.
            var addFeatureReports = ProcessRunner.RunCli(cliDllPath, solutionRoot, "add", "feature", "Reports");
            Assert.True(addFeatureReports.ExitCode == 0, $"'add feature Reports' failed:\n{addFeatureReports}");

            var bespokeCommand = ProcessRunner.RunCli(cliDllPath, solutionRoot, "add", "command", "Reports", "GenerateMonthlyReport");
            Assert.True(bespokeCommand.ExitCode == 0, $"bespoke 'add command' should be allowed without an entity:\n{bespokeCommand}");

            var bespokeQuery = ProcessRunner.RunCli(cliDllPath, solutionRoot, "add", "query", "Reports", "GetReportStatus");
            Assert.True(bespokeQuery.ExitCode == 0, $"bespoke 'add query' should be allowed without an entity:\n{bespokeQuery}");

            // `add crud` for an entity whose operations all exist is a safe no-op.
            var idempotentCrud = ProcessRunner.RunCli(cliDllPath, solutionRoot, "add", "crud", "Invoices", "Invoice");
            Assert.Equal(0, idempotentCrud.ExitCode);
            Assert.Contains("nothing to add", idempotentCrud.CombinedOutputNormalized());
        }
        finally
        {
            TempWorkspace.TryDelete(workDir);
        }
    }
}
