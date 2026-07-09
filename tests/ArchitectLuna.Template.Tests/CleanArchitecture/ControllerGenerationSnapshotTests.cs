using ArchitectLuna.Core.Model;
using ArchitectLuna.Template.Tests.Infrastructure;
using Xunit;

namespace ArchitectLuna.Template.Tests.CleanArchitecture;

/// <summary>
/// docs/requirements/004-standards-return-types.md §17, clean-architecture profile: Controllers
/// style must work identically across the split-project layout — controllers land in the Api
/// project, use the Contracts DTOs, and produce the same envelope/status-code contract as the
/// vertical-slice profile.
/// </summary>
public sealed class ControllerGenerationSnapshotTests
{
    private const string Api = "src/BillingService.Api";

    [Theory]
    [InlineData("mediatr", "efcore-postgres")]
    [InlineData("wolverine", "efcore-sqlserver")]
    public void Controllers_AreGeneratedInTheApiProject(string adapter, string persistence)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.CleanArchitectureContext(), adapter, persistence, GenerationTestHarness.InvoiceFeature(), ApiStyle.Controllers);
        var paths = files.Select(f => f.RelativePath).ToHashSet();

        foreach (var operation in new[] { "CreateInvoice", "UpdateInvoice", "DeleteInvoice", "GetInvoiceById", "GetAllInvoices" })
        {
            Assert.Contains($"{Api}/Features/Invoices/{operation}/{operation}Controller.cs", paths);
            Assert.DoesNotContain($"{Api}/Features/Invoices/{operation}/{operation}Endpoint.cs", paths);
        }
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void Controllers_UseContractsDtosAndTheActionResultExtensions(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.CleanArchitectureContext(), adapter, "efcore-postgres", GenerationTestHarness.InvoiceFeature(), ApiStyle.Controllers);

        var create = GenerationTestHarness.ContentOf(files, $"{Api}/Features/Invoices/CreateInvoice/CreateInvoiceController.cs");
        Assert.Contains("using BillingService.Contracts.Features.Invoices.CreateInvoice;", create);
        Assert.Contains("[ApiController]", create);
        Assert.Contains("[HttpPost(\"/api/invoices\")]", create);
        Assert.Contains("result.ToCreatedActionResponse(", create);
        Assert.Contains("ApiResponse<CreateInvoiceResponse>", create);

        var delete = GenerationTestHarness.ContentOf(files, $"{Api}/Features/Invoices/DeleteInvoice/DeleteInvoiceController.cs");
        Assert.Contains("result.ToNoContentActionResponse()", delete);
        Assert.DoesNotContain("result.ToProblem()", delete);
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void Routes_AreIdenticalBetweenMinimalApiAndControllersStyles(string adapter)
    {
        var minimalApiFiles = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.CleanArchitectureContext(), adapter, "efcore-postgres", GenerationTestHarness.InvoiceFeature(), ApiStyle.MinimalApi);
        var controllerFiles = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.CleanArchitectureContext(), adapter, "efcore-postgres", GenerationTestHarness.InvoiceFeature(), ApiStyle.Controllers);

        Assert.Contains("MapGet(\"/api/invoices/{id}\"", GenerationTestHarness.ContentOf(minimalApiFiles, $"{Api}/Features/Invoices/GetInvoiceById/GetInvoiceByIdEndpoint.cs"));
        Assert.Contains("HttpGet(\"/api/invoices/{id}\")", GenerationTestHarness.ContentOf(controllerFiles, $"{Api}/Features/Invoices/GetInvoiceById/GetInvoiceByIdController.cs"));

        Assert.Contains("MapGet(\"/api/invoices\"", GenerationTestHarness.ContentOf(minimalApiFiles, $"{Api}/Features/Invoices/GetAllInvoices/GetAllInvoicesEndpoint.cs"));
        Assert.Contains("HttpGet(\"/api/invoices\")", GenerationTestHarness.ContentOf(controllerFiles, $"{Api}/Features/Invoices/GetAllInvoices/GetAllInvoicesController.cs"));
    }
}
