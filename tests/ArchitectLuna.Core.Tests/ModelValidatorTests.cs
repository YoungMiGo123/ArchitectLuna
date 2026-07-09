using ArchitectLuna.Core.Model;
using ArchitectLuna.Core.Validation;
using Xunit;

namespace ArchitectLuna.Core.Tests;

public sealed class ModelValidatorTests
{
    [Fact]
    public void Validate_ValidModel_HasNoErrors()
    {
        var model = new ArchitectModel
        {
            SolutionName = "BillingService",
            Namespace = "BillingService",
            Adapter = "mediatr",
            Features = new List<FeatureModel>
            {
                new()
                {
                    Name = "Invoices",
                    Commands = new List<CommandModel> { new() { Name = "CreateInvoice" } },
                    Queries = new List<QueryModel> { new() { Name = "GetInvoiceById" } },
                },
            },
        };

        var result = ModelValidator.Validate(model);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_UnknownAdapter_ReportsError()
    {
        var model = new ArchitectModel { SolutionName = "X", Namespace = "X", Adapter = "not-real" };

        var result = ModelValidator.Validate(model);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("adapter"));
    }

    [Fact]
    public void Validate_DuplicateFeatureNames_ReportsError()
    {
        var model = new ArchitectModel
        {
            SolutionName = "X",
            Namespace = "X",
            Adapter = "mediatr",
            Features = new List<FeatureModel> { new() { Name = "Invoices" }, new() { Name = "Invoices" } },
        };

        var result = ModelValidator.Validate(model);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("duplicate feature"));
    }

    [Fact]
    public void Validate_DuplicateFieldOnCommand_ReportsError()
    {
        var model = new ArchitectModel
        {
            SolutionName = "X",
            Namespace = "X",
            Adapter = "mediatr",
            Features = new List<FeatureModel>
            {
                new()
                {
                    Name = "Invoices",
                    Commands = new List<CommandModel>
                    {
                        new()
                        {
                            Name = "CreateInvoice",
                            Fields = new List<FieldModel>
                            {
                                new() { Name = "AmountCents", Type = "long" },
                                new() { Name = "AmountCents", Type = "long" },
                            },
                        },
                    },
                },
            },
        };

        var result = ModelValidator.Validate(model);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("duplicate field"));
    }
}
