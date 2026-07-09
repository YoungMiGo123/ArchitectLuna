using ArchitectLuna.Core.Model;
using ArchitectLuna.Template.Tests.Infrastructure;
using Xunit;

namespace ArchitectLuna.Template.Tests.VerticalSlice;

/// <summary>
/// docs/requirements/002-testing-layer.md §Vertical Slice Architecture Template Tests: each
/// operation gets its own self-contained slice folder holding Request/Command/Result/Response/
/// Validator/Handler/Mappings inside the one Api project, endpoints alongside, identical for
/// both adapters.
/// </summary>
public sealed class FeatureGenerationSnapshotTests
{
    private const string Slice = "src/BillingService.Api/Features/Invoices/CreateInvoice";

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void CreateSlice_ContainsTheFullFileSet(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), adapter, "in-memory", GenerationTestHarness.InvoiceFeature());
        var paths = files.Select(f => f.RelativePath).ToHashSet();

        var expected = new[]
        {
            $"{Slice}/Contracts/CreateInvoiceRequest.cs",
            $"{Slice}/CreateInvoiceCommand.cs",
            $"{Slice}/CreateInvoiceResult.cs",
            $"{Slice}/Contracts/CreateInvoiceResponse.cs",
            $"{Slice}/CreateInvoiceValidator.cs",
            $"{Slice}/CreateInvoiceHandler.cs",
            $"{Slice}/CreateInvoiceMappings.cs",
            $"{Slice}/CreateInvoiceEndpoint.cs",
        };

        Assert.All(expected, path => Assert.Contains(path, paths));
    }

    [Fact]
    public void BothAdapters_ProduceTheIdenticalFileSet()
    {
        var feature = GenerationTestHarness.InvoiceFeature();
        var mediatr = GenerationTestHarness.GenerateFeature(GenerationTestHarness.VerticalSliceContext(), "mediatr", "in-memory", feature)
            .Select(f => f.RelativePath).OrderBy(p => p).ToList();
        var wolverine = GenerationTestHarness.GenerateFeature(GenerationTestHarness.VerticalSliceContext(), "wolverine", "in-memory", feature)
            .Select(f => f.RelativePath).OrderBy(p => p).ToList();

        Assert.Equal(mediatr, wolverine);
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void Endpoint_RouteShapeIsCorrect(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), adapter, "in-memory", GenerationTestHarness.InvoiceFeature());

        Assert.Contains("MapPost(\"/api/invoices\"", GenerationTestHarness.ContentOf(files, $"{Slice}/CreateInvoiceEndpoint.cs"));
        Assert.Contains("MapPut(\"/api/invoices/{id}\"", GenerationTestHarness.ContentOf(files, "src/BillingService.Api/Features/Invoices/UpdateInvoice/UpdateInvoiceEndpoint.cs"));
        Assert.Contains("MapDelete(\"/api/invoices/{id}\"", GenerationTestHarness.ContentOf(files, "src/BillingService.Api/Features/Invoices/DeleteInvoice/DeleteInvoiceEndpoint.cs"));
        Assert.Contains("MapGet(\"/api/invoices/{id}\"", GenerationTestHarness.ContentOf(files, "src/BillingService.Api/Features/Invoices/GetInvoiceById/GetInvoiceByIdEndpoint.cs"));
        Assert.Contains("MapGet(\"/api/invoices\"", GenerationTestHarness.ContentOf(files, "src/BillingService.Api/Features/Invoices/GetAllInvoices/GetAllInvoicesEndpoint.cs"));
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void Handler_HasProtectedRegionAndPersistenceLogic(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), adapter, "in-memory", GenerationTestHarness.InvoiceFeature());
        var handler = GenerationTestHarness.ContentOf(files, $"{Slice}/CreateInvoiceHandler.cs");

        Assert.Contains("// <architect:region name=\"handler-body\">", handler);
        Assert.Contains("// </architect:region>", handler);
        Assert.Contains("store.Save(entity.Id, entity);", handler);
        Assert.Contains("Result<CreateInvoiceResult>.Success", handler);
    }

    [Fact]
    public void MediatRMessage_ReturnsResultWrappedType()
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), "mediatr", "in-memory", GenerationTestHarness.InvoiceFeature());

        Assert.Contains(
            "IRequest<Result<CreateInvoiceResult>>",
            GenerationTestHarness.ContentOf(files, $"{Slice}/CreateInvoiceCommand.cs"));
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void Mappings_StayInsideTheSliceAndCoverBothDirections(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), adapter, "in-memory", GenerationTestHarness.InvoiceFeature());
        var mappings = GenerationTestHarness.ContentOf(files, $"{Slice}/CreateInvoiceMappings.cs");

        Assert.Contains("public static CreateInvoiceCommand ToCommand(this CreateInvoiceRequest request)", mappings);
        Assert.Contains("public static CreateInvoiceResponse ToResponse(this CreateInvoiceResult result)", mappings);
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void Validator_ValidatesTheConfiguredRules(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), adapter, "in-memory", GenerationTestHarness.InvoiceFeature());
        var validator = GenerationTestHarness.ContentOf(files, $"{Slice}/CreateInvoiceValidator.cs");

        Assert.Contains("AbstractValidator<CreateInvoiceCommand>", validator);
        Assert.Contains("RuleFor(x => x.AmountCents).GreaterThan(0);", validator);
        // Currency gets the field-type default (NotEmpty, string) plus the name-based default
        // (MaximumLength(3), name contains "currency") plus the explicit --rule, deduplicated.
        Assert.Contains("RuleFor(x => x.Currency).NotEmpty().MaximumLength(3);", validator);
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void Validator_AppliesFieldTypeDefaultsEvenWithoutExplicitRules(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), adapter, "in-memory", GenerationTestHarness.InvoiceFeature());
        var validator = GenerationTestHarness.ContentOf(files, $"{Slice}/CreateInvoiceValidator.cs");

        // CustomerId (Guid, no explicit rules) still gets a sensible default.
        Assert.Contains("RuleFor(x => x.CustomerId).NotEmpty();", validator);
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void Validator_HasNoBlankLinesFromSkippedFieldsOrRules(string adapter)
    {
        var files = GenerationTestHarness.GenerateFeature(
            GenerationTestHarness.VerticalSliceContext(), adapter, "in-memory", GenerationTestHarness.InvoiceFeature());
        var validator = GenerationTestHarness.ContentOf(files, $"{Slice}/CreateInvoiceValidator.cs");

        Assert.DoesNotContain("\n\n\n", validator);
        Assert.DoesNotContain("        \n", validator);
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void Validator_SkipsRuleForClauseCleanlyWhenAFieldHasNoRulesAtAll(string adapter)
    {
        var persistence = GenerationTestHarness.Persistence("in-memory");
        var adapterInstance = GenerationTestHarness.Adapter(adapter, persistence);
        var feature = new FeatureModel
        {
            Name = "Invoices",
            Commands =
            {
                new CommandModel
                {
                    Name = "ArchiveInvoice",
                    Kind = CommandKind.Update,
                    Fields = { new FieldModel { Name = "Silent", Type = "bool" } },
                },
            },
        };

        var files = adapterInstance.GenerateCommand(GenerationTestHarness.VerticalSliceContext(), feature, feature.Commands[0]);
        var validator = files.Single(f => f.RelativePath.EndsWith("ArchiveInvoiceValidator.cs")).Content;

        // A bool field gets no default and has no explicit rule, so the whole RuleFor clause
        // must be skipped without leaving a blank line behind (docs/requirements/003-improvements.md §4).
        Assert.DoesNotContain("RuleFor", validator);
        Assert.DoesNotContain("\n\n\n", validator);
        Assert.DoesNotContain("        \n", validator);
    }
}
