using ArchitectLuna.Template.Tests.Infrastructure;
using Xunit;

namespace ArchitectLuna.Template.Tests.CleanArchitecture;

/// <summary>
/// docs/requirements/002-testing-layer.md §Clean Architecture Template Tests, updated per
/// docs/requirements/003-improvements.md §2.2-2.3: generated code is placed into the correct
/// projects/layers — entities in Domain, handlers/validators/mappings in Application,
/// Request/Response DTOs in a `Contracts/` subfolder of each Application feature slice (not a
/// separate project), endpoints in Api, persistence in Infrastructure — and layer leaks are
/// rejected.
/// </summary>
public sealed class FeatureGenerationSnapshotTests
{
    private const string Api = "src/BillingService.Api";
    private const string Application = "src/BillingService.Application";
    private const string Domain = "src/BillingService.Domain";
    private const string Infrastructure = "src/BillingService.Infrastructure";

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void GeneratedFiles_LandInTheCorrectLayers(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.CleanArchitectureContext(), adapter, "efcore-postgres", GenerationTestHarness.InvoiceFeature());
        var paths = files.Select(f => f.RelativePath).ToHashSet();

        // Domain owns the entity.
        Assert.Contains($"{Domain}/Entities/Invoice.cs", paths);

        // Application owns the use case.
        Assert.Contains($"{Application}/Features/Invoices/CreateInvoice/CreateInvoiceCommand.cs", paths);
        Assert.Contains($"{Application}/Features/Invoices/CreateInvoice/CreateInvoiceResult.cs", paths);
        Assert.Contains($"{Application}/Features/Invoices/CreateInvoice/CreateInvoiceValidator.cs", paths);
        Assert.Contains($"{Application}/Features/Invoices/CreateInvoice/CreateInvoiceHandler.cs", paths);
        Assert.Contains($"{Application}/Features/Invoices/CreateInvoice/CreateInvoiceMappings.cs", paths);

        // Contracts is a subfolder of the owning Application slice, not a separate project.
        Assert.Contains($"{Application}/Features/Invoices/CreateInvoice/Contracts/CreateInvoiceRequest.cs", paths);
        Assert.Contains($"{Application}/Features/Invoices/CreateInvoice/Contracts/CreateInvoiceResponse.cs", paths);

        // Api owns the HTTP endpoint.
        Assert.Contains($"{Api}/Features/Invoices/CreateInvoice/CreateInvoiceEndpoint.cs", paths);

        // Infrastructure owns the persistence wiring.
        Assert.Contains($"{Infrastructure}/Configurations/InvoiceConfiguration.cs", paths);
        Assert.Contains($"{Infrastructure}/BillingServiceDbContext.cs", paths);
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void DtosNeverLandInDomain(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.CleanArchitectureContext(), adapter, "efcore-postgres", GenerationTestHarness.InvoiceFeature());

        Assert.DoesNotContain(files, f =>
            f.RelativePath.StartsWith(Domain, StringComparison.Ordinal) &&
            (f.RelativePath.EndsWith("Request.cs", StringComparison.Ordinal) || f.RelativePath.EndsWith("Response.cs", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void DomainOutput_NeverReferencesDispatcherOrPersistenceFrameworks(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.CleanArchitectureContext(), adapter, "efcore-postgres", GenerationTestHarness.InvoiceFeature());

        foreach (var file in files.Where(f => f.RelativePath.StartsWith(Domain, StringComparison.Ordinal)))
        {
            Assert.DoesNotContain("MediatR", file.Content);
            Assert.DoesNotContain("Wolverine", file.Content);
            Assert.DoesNotContain("EntityFrameworkCore", file.Content);
            Assert.DoesNotContain("Marten", file.Content);
        }
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void ApplicationHandlers_DependOnTheDbContextInterface_NotTheConcreteType(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.CleanArchitectureContext(), adapter, "efcore-postgres", GenerationTestHarness.InvoiceFeature());
        var handler = GenerationTestHarness.ContentOf(files, $"{Application}/Features/Invoices/CreateInvoice/CreateInvoiceHandler.cs");

        Assert.Contains("IBillingServiceDbContext", handler);
        // Application must never name the Infrastructure namespace — that would be an inward
        // dependency violation even if the project reference happened to exist.
        Assert.DoesNotContain("BillingService.Infrastructure", handler);
    }

    [Fact]
    public void BothAdapters_ProduceTheIdenticalFileSet()
    {
        var feature = GenerationTestHarness.InvoiceFeature();
        var mediatr = GenerationTestHarness.GenerateFeature(GenerationTestHarness.CleanArchitectureContext(), "mediatr", "efcore-postgres", feature)
            .Select(f => f.RelativePath).OrderBy(p => p).ToList();
        var wolverine = GenerationTestHarness.GenerateFeature(GenerationTestHarness.CleanArchitectureContext(), "wolverine", "efcore-postgres", feature)
            .Select(f => f.RelativePath).OrderBy(p => p).ToList();

        Assert.Equal(mediatr, wolverine);
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void Mappings_BridgeContractsAndApplication(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.CleanArchitectureContext(), adapter, "efcore-postgres", GenerationTestHarness.InvoiceFeature());
        var mappings = GenerationTestHarness.ContentOf(files, $"{Application}/Features/Invoices/CreateInvoice/CreateInvoiceMappings.cs");

        Assert.Contains("using BillingService.Application.Features.Invoices.CreateInvoice.Contracts;", mappings);
        Assert.Contains("ToCommand(this CreateInvoiceRequest request)", mappings);
        Assert.Contains("ToResponse(this CreateInvoiceResult result)", mappings);
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void MartenDocuments_LandInDomainDocuments(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.CleanArchitectureContext(), adapter, "marten", GenerationTestHarness.InvoiceFeature());

        var document = GenerationTestHarness.ContentOf(files, $"{Domain}/Documents/Invoice.cs");
        Assert.Contains("public sealed class Invoice : BaseEntity", document);
    }
}
