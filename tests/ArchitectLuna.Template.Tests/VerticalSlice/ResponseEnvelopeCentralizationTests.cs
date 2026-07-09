using ArchitectLuna.Core.Model;
using ArchitectLuna.Template.Tests.Infrastructure;
using Xunit;

namespace ArchitectLuna.Template.Tests.VerticalSlice;

/// <summary>
/// docs/requirements/004-standards-return-types.md §17: generated endpoints must never construct
/// the response envelope by hand (centralization), must carry the required OpenAPI metadata, must
/// be identical in envelope-shape regardless of the chosen persistence provider, and must be
/// structurally identical between the MediatR and Wolverine adapters (only the dispatcher call
/// itself may differ).
/// </summary>
public sealed class ResponseEnvelopeCentralizationTests
{
    private const string Features = "src/BillingService.Api/Features/Invoices";

    private static readonly string[] Operations = { "CreateInvoice", "UpdateInvoice", "DeleteInvoice", "GetInvoiceById", "GetAllInvoices" };

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void Endpoints_NeverManuallyConstructTheResponseEnvelope(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), adapter, "in-memory", GenerationTestHarness.InvoiceFeature());

        foreach (var operation in Operations)
        {
            var endpoint = GenerationTestHarness.ContentOf(files, $"{Features}/{operation}/{operation}Endpoint.cs");
            Assert.DoesNotContain("new ApiResponse<", endpoint);
            Assert.DoesNotContain("Results.Problem(", endpoint);
            Assert.DoesNotContain("Results.ValidationProblem(", endpoint);
        }
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void Controllers_NeverManuallyConstructTheResponseEnvelope(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), adapter, "in-memory", GenerationTestHarness.InvoiceFeature(), ApiStyle.Controllers);

        foreach (var operation in Operations)
        {
            var controller = GenerationTestHarness.ContentOf(files, $"{Features}/{operation}/{operation}Controller.cs");
            Assert.DoesNotContain("new ApiResponse<", controller);
            Assert.DoesNotContain("Results.Problem(", controller);
            Assert.DoesNotContain("Results.ValidationProblem(", controller);
            Assert.DoesNotContain("new ObjectResult(", controller);
        }
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void Endpoints_CarryTheRequiredOpenApiMetadataAndCentralizedMappingCalls(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), adapter, "in-memory", GenerationTestHarness.InvoiceFeature());

        var create = GenerationTestHarness.ContentOf(files, $"{Features}/CreateInvoice/CreateInvoiceEndpoint.cs");
        Assert.Contains("Produces<ApiResponse<", create);
        Assert.Contains("ToCreatedResponse", create);

        var update = GenerationTestHarness.ContentOf(files, $"{Features}/UpdateInvoice/UpdateInvoiceEndpoint.cs");
        Assert.Contains("Produces<ApiResponse<", update);
        Assert.Contains("ToOkResponse", update);

        var delete = GenerationTestHarness.ContentOf(files, $"{Features}/DeleteInvoice/DeleteInvoiceEndpoint.cs");
        Assert.Contains("Produces<ApiResponse<object>>", delete);
        Assert.Contains("ToNoContentResponse", delete);
    }

    [Theory]
    [InlineData("in-memory")]
    [InlineData("efcore-postgres")]
    [InlineData("efcore-sqlserver")]
    [InlineData("marten")]
    [InlineData("none")]
    public void Persistence_DoesNotAlterTheGeneratedEndpointsEnvelopeContent(string persistence)
    {
        // Ground truth from the persistence proven-orthogonal in-memory case.
        var baseline = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), "mediatr", "in-memory", GenerationTestHarness.InvoiceFeature());
        var underTest = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), "mediatr", persistence, GenerationTestHarness.InvoiceFeature());

        foreach (var operation in Operations)
        {
            var baselineEndpoint = GenerationTestHarness.ContentOf(baseline, $"{Features}/{operation}/{operation}Endpoint.cs");
            var underTestEndpoint = GenerationTestHarness.ContentOf(underTest, $"{Features}/{operation}/{operation}Endpoint.cs");

            // Endpoint files don't reference persistence at all, so switching --persistence must
            // leave them byte-for-byte identical (only handler bodies differ by provider).
            Assert.Equal(baselineEndpoint, underTestEndpoint);
        }
    }

    [Theory]
    [InlineData(ApiStyle.MinimalApi)]
    [InlineData(ApiStyle.Controllers)]
    public void MediatRAndWolverine_ProduceStructurallyIdenticalEnvelopeMapping(ApiStyle apiStyle)
    {
        var mediatrFiles = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), "mediatr", "in-memory", GenerationTestHarness.InvoiceFeature(), apiStyle);
        var wolverineFiles = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), "wolverine", "in-memory", GenerationTestHarness.InvoiceFeature(), apiStyle);

        var fileSuffix = apiStyle == ApiStyle.Controllers ? "Controller" : "Endpoint";

        foreach (var operation in Operations)
        {
            var mediatrContent = GenerationTestHarness.ContentOf(mediatrFiles, $"{Features}/{operation}/{operation}{fileSuffix}.cs");
            var wolverineContent = GenerationTestHarness.ContentOf(wolverineFiles, $"{Features}/{operation}/{operation}{fileSuffix}.cs");

            // The dispatcher-specific lines (using/type/param and the actual dispatch call) are
            // allowed to differ; everything else — routes, HTTP attributes, ProducesResponseType/
            // Produces<ApiResponse<...>> metadata, and the success/failure mapping calls — must be
            // identical, since the response envelope contract is adapter-independent.
            foreach (var line in mediatrContent.Split('\n'))
            {
                var trimmed = line.Trim();
                if (IsDispatcherSpecificLine(trimmed))
                {
                    continue;
                }

                Assert.Contains(trimmed, wolverineContent);
            }
        }
    }

    private static bool IsDispatcherSpecificLine(string trimmedLine) =>
        trimmedLine.Length == 0
        || trimmedLine.StartsWith("using MediatR", StringComparison.Ordinal)
        || trimmedLine.StartsWith("using Wolverine", StringComparison.Ordinal)
        || trimmedLine.Contains("ISender")
        || trimmedLine.Contains("IMessageBus")
        || trimmedLine.Contains("sender.")
        || trimmedLine.Contains("bus.");
}
