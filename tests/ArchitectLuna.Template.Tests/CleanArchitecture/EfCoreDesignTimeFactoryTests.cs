using ArchitectLuna.Template.Tests.Infrastructure;
using Xunit;

namespace ArchitectLuna.Template.Tests.CleanArchitecture;

/// <summary>
/// docs/requirements/003-improvements.md §10.1, §10.2: EF Core solutions ship a design-time
/// DbContext factory and reference Microsoft.EntityFrameworkCore.Design, so `dotnet ef migrations
/// add`/`database update` work without a runtime DI container.
/// </summary>
public sealed class EfCoreDesignTimeFactoryTests
{
    private const string Infrastructure = "src/BillingService.Infrastructure";

    [Theory]
    [InlineData("efcore-postgres", "UseNpgsql")]
    [InlineData("efcore-sqlserver", "UseSqlServer")]
    public void DesignTimeFactory_IsGeneratedAndUsesTheRightProviderCall(string persistence, string expectedProviderCall)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.CleanArchitectureContext(), "mediatr", persistence, GenerationTestHarness.InvoiceFeature());
        var factory = GenerationTestHarness.ContentOf(files, $"{Infrastructure}/Persistence/BillingServiceDbContextFactory.cs");

        Assert.Contains("IDesignTimeDbContextFactory<BillingServiceDbContext>", factory);
        Assert.Contains("public BillingServiceDbContext CreateDbContext(string[] args)", factory);
        Assert.Contains(expectedProviderCall, factory);
    }

    [Theory]
    [InlineData("efcore-postgres")]
    [InlineData("efcore-sqlserver")]
    public void RequiredPackages_IncludeEfCoreDesign(string persistence)
    {
        var provider = GenerationTestHarness.Persistence(persistence);

        Assert.Contains("Microsoft.EntityFrameworkCore.Design", provider.RequiredPackages);
    }
}
