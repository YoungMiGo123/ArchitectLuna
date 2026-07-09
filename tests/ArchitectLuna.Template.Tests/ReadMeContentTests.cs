using ArchitectLuna.Cli.Scaffolding;
using ArchitectLuna.Core.Model;
using Xunit;

namespace ArchitectLuna.Template.Tests;

/// <summary>
/// docs/requirements/003-improvements.md §13: the generated README documents the database apply
/// mode, how to add a field, EF Core migration commands (EF Core solutions only), and Marten
/// schema handling (Marten solutions only).
/// </summary>
public sealed class ReadMeContentTests
{
    [Fact]
    public void ReadMe_DocumentsApplyModeAndAddFieldWorkflow()
    {
        var readme = InfrastructureFiles.ReadMe("BillingService", "mediatr", "in-memory", SolutionLayout.CleanArchitecture, DatabaseApplyMode.OnStartup);

        Assert.Contains("database apply mode: `on-startup`", readme);
        Assert.Contains("architect-luna add field Payments PaymentRequest Reference:string", readme);
        Assert.Contains("docker compose up --build", readme);
        Assert.Contains("architect:region", readme);
    }

    [Fact]
    public void ReadMe_EfCoreSolution_IncludesMigrationCommands()
    {
        var readme = InfrastructureFiles.ReadMe("BillingService", "mediatr", "efcore-postgres", SolutionLayout.CleanArchitecture);

        Assert.Contains("dotnet ef migrations add Initial", readme);
        Assert.Contains("--project src/BillingService.Infrastructure", readme);
        Assert.Contains("--startup-project src/BillingService.Api", readme);
        Assert.Contains("dotnet ef database update", readme);
        Assert.DoesNotContain("Marten schema handling", readme);
    }

    [Fact]
    public void ReadMe_MartenSolution_IncludesSchemaHandlingNote_NotEfCommands()
    {
        var readme = InfrastructureFiles.ReadMe("BillingService", "mediatr", "marten", SolutionLayout.CleanArchitecture);

        Assert.Contains("Marten schema handling", readme);
        Assert.DoesNotContain("dotnet ef migrations add", readme);
    }

    [Fact]
    public void ReadMe_InMemorySolution_HasNeitherEfNorMartenSection()
    {
        var readme = InfrastructureFiles.ReadMe("BillingService", "mediatr", "in-memory", SolutionLayout.CleanArchitecture);

        Assert.DoesNotContain("dotnet ef migrations add", readme);
        Assert.DoesNotContain("Marten schema handling", readme);
    }
}
