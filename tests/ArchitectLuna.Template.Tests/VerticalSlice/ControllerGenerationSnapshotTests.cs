using ArchitectLuna.Core.Model;
using ArchitectLuna.Template.Tests.Infrastructure;
using Xunit;

namespace ArchitectLuna.Template.Tests.VerticalSlice;

/// <summary>
/// docs/requirements/004-standards-return-types.md §17: Controllers-style generation must produce
/// [ApiController] classes with the same envelope/status-code contract as Minimal API, and the
/// two styles must expose byte-identical routes for the same feature (URL parity is a hard
/// requirement — clients should never see a different URL shape depending on --api-style).
/// </summary>
public sealed class ControllerGenerationSnapshotTests
{
    private const string Features = "src/BillingService.Api/Features/Invoices";

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void Controllers_AreGeneratedInsteadOfEndpoints(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), adapter, "in-memory", GenerationTestHarness.InvoiceFeature(), ApiStyle.Controllers);
        var paths = files.Select(f => f.RelativePath).ToHashSet();

        foreach (var operation in new[] { "CreateInvoice", "UpdateInvoice", "DeleteInvoice", "GetInvoiceById", "GetAllInvoices" })
        {
            Assert.Contains($"{Features}/{operation}/{operation}Controller.cs", paths);
            Assert.DoesNotContain($"{Features}/{operation}/{operation}Endpoint.cs", paths);
        }
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void Controllers_CarryApiControllerAttributeAndCorrectHttpVerbAttributes(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), adapter, "in-memory", GenerationTestHarness.InvoiceFeature(), ApiStyle.Controllers);

        var create = GenerationTestHarness.ContentOf(files, $"{Features}/CreateInvoice/CreateInvoiceController.cs");
        Assert.Contains("[ApiController]", create);
        Assert.Contains("[HttpPost(\"/api/invoices\")]", create);

        var update = GenerationTestHarness.ContentOf(files, $"{Features}/UpdateInvoice/UpdateInvoiceController.cs");
        Assert.Contains("[ApiController]", update);
        Assert.Contains("[HttpPut(\"/api/invoices/{id}\")]", update);

        var delete = GenerationTestHarness.ContentOf(files, $"{Features}/DeleteInvoice/DeleteInvoiceController.cs");
        Assert.Contains("[ApiController]", delete);
        Assert.Contains("[HttpDelete(\"/api/invoices/{id}\")]", delete);

        var getById = GenerationTestHarness.ContentOf(files, $"{Features}/GetInvoiceById/GetInvoiceByIdController.cs");
        Assert.Contains("[ApiController]", getById);
        Assert.Contains("[HttpGet(\"/api/invoices/{id}\")]", getById);

        var getAll = GenerationTestHarness.ContentOf(files, $"{Features}/GetAllInvoices/GetAllInvoicesController.cs");
        Assert.Contains("[ApiController]", getAll);
        Assert.Contains("[HttpGet(\"/api/invoices\")]", getAll);
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void Controllers_DeclareProducesResponseTypeForSuccessAndEveryFailureShape(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), adapter, "in-memory", GenerationTestHarness.InvoiceFeature(), ApiStyle.Controllers);

        var create = GenerationTestHarness.ContentOf(files, $"{Features}/CreateInvoice/CreateInvoiceController.cs");
        Assert.Contains("[ProducesResponseType(typeof(ApiResponse<CreateInvoiceResponse>), StatusCodes.Status201Created)]", create);
        Assert.Contains("[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]", create);
        Assert.Contains("[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]", create);
        Assert.Contains("[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]", create);
        Assert.Contains("[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]", create);

        var delete = GenerationTestHarness.ContentOf(files, $"{Features}/DeleteInvoice/DeleteInvoiceController.cs");
        Assert.Contains("[ProducesResponseType(StatusCodes.Status204NoContent)]", delete);
        Assert.Contains("[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]", delete);
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void Controllers_UseActionResultExtensions_NotTheMinimalApiOnes(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), adapter, "in-memory", GenerationTestHarness.InvoiceFeature(), ApiStyle.Controllers);

        Assert.Contains("result.ToCreatedActionResponse(value => $\"/api/invoices/{value.Id}\", value => value.ToResponse())", GenerationTestHarness.ContentOf(files, $"{Features}/CreateInvoice/CreateInvoiceController.cs"));
        Assert.Contains("result.ToOkActionResponse(value => value.ToResponse())", GenerationTestHarness.ContentOf(files, $"{Features}/UpdateInvoice/UpdateInvoiceController.cs"));
        Assert.Contains("result.ToNoContentActionResponse()", GenerationTestHarness.ContentOf(files, $"{Features}/DeleteInvoice/DeleteInvoiceController.cs"));
        Assert.Contains("result.ToOkActionResponse(value => value.ToResponse())", GenerationTestHarness.ContentOf(files, $"{Features}/GetInvoiceById/GetInvoiceByIdController.cs"));
        Assert.Contains("result.ToOkActionResponse(value => new", GenerationTestHarness.ContentOf(files, $"{Features}/GetAllInvoices/GetAllInvoicesController.cs"));

        foreach (var operation in new[] { "CreateInvoice", "UpdateInvoice", "DeleteInvoice", "GetInvoiceById", "GetAllInvoices" })
        {
            var controller = GenerationTestHarness.ContentOf(files, $"{Features}/{operation}/{operation}Controller.cs");
            Assert.DoesNotContain("ToOkResponse", controller);
            Assert.DoesNotContain("ToCreatedResponse", controller);
            Assert.DoesNotContain("ToNoContentResponse", controller);
        }
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void Controllers_ValidationFailures_UseTheActionResultValidationMapper(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), adapter, "in-memory", GenerationTestHarness.InvoiceFeature(), ApiStyle.Controllers);

        var create = GenerationTestHarness.ContentOf(files, $"{Features}/CreateInvoice/CreateInvoiceController.cs");
        Assert.Contains("validationResult.ToValidationActionErrorResponse()", create);
        Assert.DoesNotContain("ToValidationErrorResponse()", create);
        Assert.DoesNotContain("Results.ValidationProblem(", create);
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void Routes_AreIdenticalBetweenMinimalApiAndControllersStyles(string adapter)
    {
        var minimalApiFiles = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), adapter, "in-memory", GenerationTestHarness.InvoiceFeature(), ApiStyle.MinimalApi);
        var controllerFiles = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), adapter, "in-memory", GenerationTestHarness.InvoiceFeature(), ApiStyle.Controllers);

        var routes = new (string Operation, string MinimalApiPattern, string ControllerPattern)[]
        {
            ("CreateInvoice", "MapPost(\"/api/invoices\"", "HttpPost(\"/api/invoices\")"),
            ("UpdateInvoice", "MapPut(\"/api/invoices/{id}\"", "HttpPut(\"/api/invoices/{id}\")"),
            ("DeleteInvoice", "MapDelete(\"/api/invoices/{id}\"", "HttpDelete(\"/api/invoices/{id}\")"),
            ("GetInvoiceById", "MapGet(\"/api/invoices/{id}\"", "HttpGet(\"/api/invoices/{id}\")"),
            ("GetAllInvoices", "MapGet(\"/api/invoices\"", "HttpGet(\"/api/invoices\")"),
        };

        foreach (var (operation, minimalApiPattern, controllerPattern) in routes)
        {
            var minimalApiContent = GenerationTestHarness.ContentOf(minimalApiFiles, $"{Features}/{operation}/{operation}Endpoint.cs");
            var controllerContent = GenerationTestHarness.ContentOf(controllerFiles, $"{Features}/{operation}/{operation}Controller.cs");

            Assert.Contains(minimalApiPattern, minimalApiContent);
            Assert.Contains(controllerPattern, controllerContent);

            // The route string itself (the literal URL, independent of the mapping method) must be
            // identical between the two styles — extract it out of each pattern and compare.
            var minimalApiRoute = ExtractRoute(minimalApiPattern);
            var controllerRoute = ExtractRoute(controllerPattern);
            Assert.Equal(minimalApiRoute, controllerRoute);
        }
    }

    private static string ExtractRoute(string mappingPattern)
    {
        var start = mappingPattern.IndexOf('"') + 1;
        var end = mappingPattern.LastIndexOf('"');
        return mappingPattern.Substring(start, end - start);
    }
}
