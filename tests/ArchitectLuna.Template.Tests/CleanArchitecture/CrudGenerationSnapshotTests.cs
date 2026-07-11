using ArchitectLuna.Template.Tests.Infrastructure;
using Xunit;

namespace ArchitectLuna.Template.Tests.CleanArchitecture;

/// <summary>
/// docs/requirements/002-testing-layer.md §CRUD Generation Tests, clean-architecture profile:
/// entity-outward synthesis produces all five operations across the correct projects, with real
/// persistence logic and Result-based not-found handling.
/// </summary>
public sealed class CrudGenerationSnapshotTests
{
    private const string Application = "src/BillingService.Application";
    private const string Api = "src/BillingService.Api";

    [Theory]
    [InlineData("mediatr", "efcore-postgres")]
    [InlineData("wolverine", "efcore-sqlserver")]
    [InlineData("mediatr", "marten")]
    [InlineData("wolverine", "in-memory")]
    public void AllFiveOperations_AreGeneratedForEveryPersistenceProvider(string adapter, string persistence)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.CleanArchitectureContext(), adapter, persistence, GenerationTestHarness.InvoiceFeature());
        var paths = files.Select(f => f.RelativePath).ToHashSet();

        foreach (var operation in new[] { "CreateInvoice", "UpdateInvoice", "DeleteInvoice", "GetInvoiceById", "GetAllInvoices" })
        {
            Assert.Contains($"{Application}/Features/Invoices/{operation}/{operation}Handler.cs", paths);
            Assert.Contains($"{Api}/Features/Invoices/{operation}/{operation}Endpoint.cs", paths);
        }
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void Validators_ExistOnlyForCreateAndUpdate(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.CleanArchitectureContext(), adapter, "efcore-postgres", GenerationTestHarness.InvoiceFeature());
        var paths = files.Select(f => f.RelativePath).ToHashSet();

        Assert.Contains($"{Application}/Features/Invoices/CreateInvoice/CreateInvoiceValidator.cs", paths);
        Assert.Contains($"{Application}/Features/Invoices/UpdateInvoice/UpdateInvoiceValidator.cs", paths);
        Assert.DoesNotContain($"{Application}/Features/Invoices/DeleteInvoice/DeleteInvoiceValidator.cs", paths);
        Assert.DoesNotContain($"{Application}/Features/Invoices/GetInvoiceById/GetInvoiceByIdValidator.cs", paths);
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void EfCoreHandlers_ContainRealPersistenceCallsAndResultOutcomes(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.CleanArchitectureContext(), adapter, "efcore-postgres", GenerationTestHarness.InvoiceFeature());

        var create = GenerationTestHarness.ContentOf(files, $"{Application}/Features/Invoices/CreateInvoice/CreateInvoiceHandler.cs");
        Assert.Contains("dbContext.Invoices.Add(entity);", create);
        Assert.Contains("await dbContext.SaveChangesAsync(cancellationToken);", create);
        Assert.Contains("Result<CreateInvoiceResult>.Success", create);

        var getById = GenerationTestHarness.ContentOf(files, $"{Application}/Features/Invoices/GetInvoiceById/GetInvoiceByIdHandler.cs");
        Assert.Contains("Error.NotFound(", getById);
        Assert.DoesNotContain("throw new KeyNotFoundException", getById);
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void GetByIdRouteHasId_GetAllRouteIsCollection(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.CleanArchitectureContext(), adapter, "efcore-postgres", GenerationTestHarness.InvoiceFeature());

        Assert.Contains("MapGet(\"/api/invoices/{id}\"", GenerationTestHarness.ContentOf(files, $"{Api}/Features/Invoices/GetInvoiceById/GetInvoiceByIdEndpoint.cs"));
        Assert.Contains("MapGet(\"/api/invoices\"", GenerationTestHarness.ContentOf(files, $"{Api}/Features/Invoices/GetAllInvoices/GetAllInvoicesEndpoint.cs"));
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void Endpoints_UseTheContractsDtosAndStatusCodePolicy(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.CleanArchitectureContext(), adapter, "efcore-postgres", GenerationTestHarness.InvoiceFeature());

        var create = GenerationTestHarness.ContentOf(files, $"{Api}/Features/Invoices/CreateInvoice/CreateInvoiceEndpoint.cs");
        Assert.Contains("using BillingService.Application.Features.Invoices.CreateInvoice.Contracts;", create);
        Assert.Contains("CreateInvoiceRequest request", create);
        Assert.Contains("request.ToCommand()", create);
        Assert.Contains("result.ToCreatedResponse(", create);
        Assert.Contains("ApiResponse<CreateInvoiceResponse>", create);

        var delete = GenerationTestHarness.ContentOf(files, $"{Api}/Features/Invoices/DeleteInvoice/DeleteInvoiceEndpoint.cs");
        Assert.Contains("result.ToNoContentResponse()", delete);
        Assert.DoesNotContain("result.ToProblem()", delete);
    }
}
