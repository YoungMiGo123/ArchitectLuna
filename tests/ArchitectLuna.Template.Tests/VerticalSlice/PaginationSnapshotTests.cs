using ArchitectLuna.Template.Tests.Infrastructure;
using Xunit;

namespace ArchitectLuna.Template.Tests.VerticalSlice;

/// <summary>
/// docs/plans/002-crud-getall-pagination.md: a CRUD-synthesized GetAll query pages instead of
/// loading every row, identically across adapters and persistence providers. The route stays the
/// plain collection route (design decision 2 in the plan) — Page/PageSize never join
/// <see cref="ArchitectLuna.Core.Model.QueryModel.Params"/>, so <c>RouteInference</c> is
/// unaffected.
/// </summary>
public sealed class PaginationSnapshotTests
{
    private const string Slice = "src/BillingService.Api/Features/Invoices/GetAllInvoices";

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void Message_CarriesPageAndPageSize(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), adapter, "in-memory", GenerationTestHarness.InvoiceFeature());
        var message = GenerationTestHarness.ContentOf(files, $"{Slice}/GetAllInvoicesQuery.cs");

        Assert.Contains("int Page", message);
        Assert.Contains("int PageSize", message);
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void ResultType_IsWrappedInPagedResult(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), adapter, "in-memory", GenerationTestHarness.InvoiceFeature());
        var handler = GenerationTestHarness.ContentOf(files, $"{Slice}/GetAllInvoicesHandler.cs");

        Assert.Contains("Result<PagedResult<GetAllInvoicesResult>>", handler);
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void Route_StaysThePlainCollectionRoute(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), adapter, "in-memory", GenerationTestHarness.InvoiceFeature());
        var endpoint = GenerationTestHarness.ContentOf(files, $"{Slice}/GetAllInvoicesEndpoint.cs");

        Assert.Contains("MapGet(\"/api/invoices\"", endpoint);
        Assert.Contains("[AsParameters] GetAllInvoicesQuery query", endpoint);
    }

    [Theory]
    [InlineData("mediatr", "in-memory")]
    [InlineData("wolverine", "in-memory")]
    [InlineData("mediatr", "efcore-postgres")]
    [InlineData("wolverine", "efcore-sqlserver")]
    [InlineData("mediatr", "marten")]
    [InlineData("wolverine", "marten")]
    public void HandlerBody_SkipsAndTakes_WithClampedDefaults(string adapter, string persistence)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), adapter, persistence, GenerationTestHarness.InvoiceFeature());
        var handler = GenerationTestHarness.ContentOf(files, $"{Slice}/GetAllInvoicesHandler.cs");

        Assert.Contains("message.Page <= 0 ? 1 : message.Page", handler);
        Assert.Contains("message.PageSize <= 0 ? 20 : message.PageSize", handler);
        Assert.Contains(".Skip((page - 1) * pageSize)", handler);
        Assert.Contains(".Take(pageSize)", handler);
        Assert.Contains("PagedResult<GetAllInvoicesResult>", handler);
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void Endpoint_ProjectsItemsAndPagingMetadata(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), adapter, "in-memory", GenerationTestHarness.InvoiceFeature());
        var endpoint = GenerationTestHarness.ContentOf(files, $"{Slice}/GetAllInvoicesEndpoint.cs");

        Assert.Contains("items = result.Value.Items.Select(item => item.ToResponse()).ToList()", endpoint);
        Assert.Contains("result.Value.Page", endpoint);
        Assert.Contains("result.Value.PageSize", endpoint);
        Assert.Contains("result.Value.TotalCount", endpoint);
        Assert.Contains("result.Value.TotalPages", endpoint);
        Assert.Contains("result.Value.HasNextPage", endpoint);
        Assert.Contains("result.Value.HasPreviousPage", endpoint);
    }
}
