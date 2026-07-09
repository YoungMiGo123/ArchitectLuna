using ArchitectLuna.EndToEnd.Tests.Infrastructure;
using Xunit;

namespace ArchitectLuna.EndToEnd.Tests;

/// <summary>
/// Same full-pipeline coverage as <see cref="GeneratedSolutionBuildTests"/> but for
/// `--architecture clean-architecture`: scaffold, add a feature/entity, generate, and build the
/// real four-project solution (Api/Application/Domain/Infrastructure) plus both test projects.
/// Exists specifically to catch what a vertical-slice-only test suite can't: Application
/// referencing Infrastructure directly (breaking the dependency rule), a persistence type used in
/// Program.cs before any entity exists, or a namespace `using` for a folder that has no files in it
/// yet — all bugs that only show up once source is actually split across separate projects.
/// </summary>
public sealed class CleanArchitectureBuildTests
{
    private const string SolutionName = "TestCleanApi";
    private const string FeatureName = "Widgets";
    private const string EntityName = "Widget";

    [Theory]
    [Trait("Category", "EndToEnd")]
    [InlineData("mediatr", "efcore-postgres")]
    [InlineData("wolverine", "marten")]
    [InlineData("mediatr", "none")]
    public void CleanArchitectureSolution_Compiles_WithProperLayering(string adapter, string persistence)
    {
        var cliDllPath = CliLocator.ResolveCliDllPath();
        var workDir = TempWorkspace.CreateUnique();
        try
        {
            var newApi = ProcessRunner.RunCli(cliDllPath, workDir, "new", "api", SolutionName, "--adapter", adapter, "--persistence", persistence, "--architecture", "clean-architecture");
            Assert.True(newApi.ExitCode == 0, $"'new api' (adapter={adapter}, persistence={persistence}) failed:\n{newApi}");

            var solutionRoot = Path.Combine(workDir, SolutionName);

            // The solution must compile immediately, before any feature is added — Program.cs
            // already references persistence types (e.g. the DbContext) as soon as a provider is
            // configured, so they must already exist with zero entities.
            var freshBuild = ProcessRunner.RunDotnetBuild(solutionRoot, TimeSpan.FromMinutes(5));
            Assert.True(freshBuild.ExitCode == 0, $"'dotnet build' of the freshly scaffolded solution (adapter={adapter}, persistence={persistence}) failed:\n{freshBuild}");

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

            AssertProperLayering(solutionRoot, persistence);

            var build = ProcessRunner.RunDotnetBuild(solutionRoot, TimeSpan.FromMinutes(5));
            Assert.True(build.ExitCode == 0, $"'dotnet build' after generate (adapter={adapter}, persistence={persistence}) failed:\n{build}");
        }
        finally
        {
            TempWorkspace.TryDelete(workDir);
        }
    }

    /// <summary>Entities live in Domain, messages/handlers/validators in Application, endpoints in Api — never mixed up across projects.</summary>
    private static void AssertProperLayering(string solutionRoot, string persistence)
    {
        // NullPersistenceGenerator (--persistence none) never emits an entity file at all — only
        // the message/handler/validator/endpoint shape exists regardless of persistence provider.
        if (persistence != "none")
        {
            // Marten writes plain document classes to a Documents/ subfolder rather than
            // Entities/ — see MartenPersistenceGenerator vs EfCorePersistenceGenerator/
            // InMemoryPersistenceGenerator.
            var domainEntitySubfolder = persistence == "marten" ? "Documents" : "Entities";
            var domainEntityPath = Path.Combine(solutionRoot, "src", $"{SolutionName}.Domain", domainEntitySubfolder, $"{EntityName}.cs");
            Assert.True(File.Exists(domainEntityPath), $"Expected entity in Domain project at {domainEntityPath}");
        }

        var applicationHandlerPath = Path.Combine(solutionRoot, "src", $"{SolutionName}.Application", "Features", FeatureName, $"Create{EntityName}", $"Create{EntityName}Handler.cs");
        var apiEndpointPath = Path.Combine(solutionRoot, "src", $"{SolutionName}.Api", "Features", FeatureName, $"Create{EntityName}", $"Create{EntityName}Endpoint.cs");

        Assert.True(File.Exists(applicationHandlerPath), $"Expected handler in Application project at {applicationHandlerPath}");
        Assert.True(File.Exists(apiEndpointPath), $"Expected endpoint in Api project at {apiEndpointPath}");
    }
}
