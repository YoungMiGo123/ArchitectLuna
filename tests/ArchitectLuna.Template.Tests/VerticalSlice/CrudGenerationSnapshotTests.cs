using ArchitectLuna.Template.Tests.Infrastructure;
using Xunit;

namespace ArchitectLuna.Template.Tests.VerticalSlice;

/// <summary>
/// docs/requirements/002-testing-layer.md §CRUD Generation Tests: one entity synthesizes all
/// five operations; validators only where a body exists; delete stays minimal; success maps to
/// 201/200/204 and failures go through ToProblem(); `--persistence none` leaves the protected
/// placeholder.
/// </summary>
public sealed class CrudGenerationSnapshotTests
{
    private const string Features = "src/BillingService.Api/Features/Invoices";

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void AllFiveOperations_AreGenerated(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), adapter, "in-memory", GenerationTestHarness.InvoiceFeature());
        var paths = files.Select(f => f.RelativePath).ToHashSet();

        foreach (var operation in new[] { "CreateInvoice", "UpdateInvoice", "DeleteInvoice", "GetInvoiceById", "GetAllInvoices" })
        {
            Assert.Contains($"{Features}/{operation}/{operation}Handler.cs", paths);
            Assert.Contains($"{Features}/{operation}/{operation}Endpoint.cs", paths);
        }
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void Validators_ExistOnlyForCreateAndUpdate(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), adapter, "in-memory", GenerationTestHarness.InvoiceFeature());
        var paths = files.Select(f => f.RelativePath).ToHashSet();

        Assert.Contains($"{Features}/CreateInvoice/CreateInvoiceValidator.cs", paths);
        Assert.Contains($"{Features}/UpdateInvoice/UpdateInvoiceValidator.cs", paths);
        Assert.DoesNotContain($"{Features}/DeleteInvoice/DeleteInvoiceValidator.cs", paths);
        Assert.DoesNotContain($"{Features}/GetInvoiceById/GetInvoiceByIdValidator.cs", paths);
        Assert.DoesNotContain($"{Features}/GetAllInvoices/GetAllInvoicesValidator.cs", paths);
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void Delete_StaysMinimal_NoRequestResponseOrMappings(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), adapter, "in-memory", GenerationTestHarness.InvoiceFeature());
        var paths = files.Select(f => f.RelativePath).ToHashSet();

        Assert.Contains($"{Features}/DeleteInvoice/DeleteInvoiceCommand.cs", paths);
        Assert.Contains($"{Features}/DeleteInvoice/DeleteInvoiceResult.cs", paths);
        Assert.DoesNotContain($"{Features}/DeleteInvoice/DeleteInvoiceRequest.cs", paths);
        Assert.DoesNotContain($"{Features}/DeleteInvoice/DeleteInvoiceResponse.cs", paths);
        Assert.DoesNotContain($"{Features}/DeleteInvoice/DeleteInvoiceMappings.cs", paths);
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void Endpoints_MapResultsToConsistentStatusCodes(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), adapter, "in-memory", GenerationTestHarness.InvoiceFeature());

        Assert.Contains("result.ToCreatedResponse(value => $\"/api/invoices/{value.Id}\", value => value.ToResponse())", GenerationTestHarness.ContentOf(files, $"{Features}/CreateInvoice/CreateInvoiceEndpoint.cs"));
        Assert.Contains("result.ToOkResponse(value => value.ToResponse())", GenerationTestHarness.ContentOf(files, $"{Features}/UpdateInvoice/UpdateInvoiceEndpoint.cs"));
        Assert.Contains("result.ToNoContentResponse()", GenerationTestHarness.ContentOf(files, $"{Features}/DeleteInvoice/DeleteInvoiceEndpoint.cs"));
        Assert.Contains("result.ToOkResponse(value => new", GenerationTestHarness.ContentOf(files, $"{Features}/GetAllInvoices/GetAllInvoicesEndpoint.cs"));

        // Every endpoint centralizes both success and failure through ResultExtensions and never
        // hand-constructs an ApiResponse/problem response itself.
        foreach (var operation in new[] { "CreateInvoice", "UpdateInvoice", "DeleteInvoice", "GetInvoiceById", "GetAllInvoices" })
        {
            var endpoint = GenerationTestHarness.ContentOf(files, $"{Features}/{operation}/{operation}Endpoint.cs");
            Assert.Contains("ApiResponse<", endpoint);
            Assert.DoesNotContain("new ApiResponse<", endpoint);
            Assert.DoesNotContain("result.ToProblem()", endpoint);
            Assert.DoesNotContain("Results.Problem(", endpoint);
        }
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void NonePersistence_LeavesProtectedPlaceholderHandlers(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), adapter, "none", GenerationTestHarness.InvoiceFeature());
        var handler = GenerationTestHarness.ContentOf(files, $"{Features}/CreateInvoice/CreateInvoiceHandler.cs");

        Assert.Contains("// <architect:region name=\"handler-body\">", handler);
        Assert.Contains("throw new NotImplementedException();", handler);
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void PersistenceHandlers_ReturnNotFoundResultsInsteadOfThrowing(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), adapter, "in-memory", GenerationTestHarness.InvoiceFeature());
        var handler = GenerationTestHarness.ContentOf(files, $"{Features}/GetInvoiceById/GetInvoiceByIdHandler.cs");

        Assert.Contains("Error.NotFound(", handler);
        Assert.DoesNotContain("throw new KeyNotFoundException", handler);
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void GetAll_IsPaged_MessageResultAndEndpointAllUsePagedResult(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), adapter, "in-memory", GenerationTestHarness.InvoiceFeature());

        // Message carries Page/PageSize bound from the query string; result type is PagedResult<T>.
        var message = GenerationTestHarness.ContentOf(files, $"{Features}/GetAllInvoices/GetAllInvoicesQuery.cs");
        Assert.Contains("int Page", message);
        Assert.Contains("int PageSize", message);

        var handler = GenerationTestHarness.ContentOf(files, $"{Features}/GetAllInvoices/GetAllInvoicesHandler.cs");
        Assert.Contains("Result<PagedResult<GetAllInvoicesResult>>", handler);
        Assert.Contains("Skip((page - 1) * pageSize).Take(pageSize)", handler);

        var endpoint = GenerationTestHarness.ContentOf(files, $"{Features}/GetAllInvoices/GetAllInvoicesEndpoint.cs");
        // Collection route preserved; page/pageSize bound via [AsParameters]; paging maps into a typed PagedResponse<T>.
        Assert.Contains("MapGet(\"/api/invoices\"", endpoint);
        Assert.Contains("new BillingService.Common.PagedResponse<GetAllInvoicesResponse>", endpoint);
        Assert.Contains("value.Page, value.PageSize, value.TotalCount", endpoint);
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void GeneratedEntity_InheritsBaseEntity(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), adapter, "in-memory", GenerationTestHarness.InvoiceFeature());
        var entity = GenerationTestHarness.ContentOf(files, "src/BillingService.Api/Persistence/Entities/Invoice.cs");

        Assert.Contains("public sealed class Invoice : BaseEntity", entity);
        Assert.DoesNotContain("public Guid Id", entity);
    }
}
