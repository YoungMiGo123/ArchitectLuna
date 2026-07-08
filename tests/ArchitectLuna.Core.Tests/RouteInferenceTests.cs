using ArchitectLuna.Core.Model;
using ArchitectLuna.Core.Naming;
using Xunit;

namespace ArchitectLuna.Core.Tests;

public sealed class RouteInferenceTests
{
    [Fact]
    public void QueryRoute_SingleIdParam_UsesRouteSegment()
    {
        var feature = new FeatureModel { Name = "Invoices" };
        var query = new QueryModel
        {
            Name = "GetInvoiceById",
            Params = new List<ParamModel> { new() { Name = "Id", Type = "Guid" } },
        };

        Assert.Equal("/api/invoices/{id}", RouteInference.QueryRoute(feature, query));
    }

    [Fact]
    public void QueryRoute_MultipleParams_AppendsKebabQueryName()
    {
        var feature = new FeatureModel { Name = "Invoices" };
        var query = new QueryModel
        {
            Name = "SearchInvoices",
            Params = new List<ParamModel>
            {
                new() { Name = "CustomerId", Type = "Guid" },
                new() { Name = "Status", Type = "string" },
            },
        };

        Assert.Equal("/api/invoices/search-invoices", RouteInference.QueryRoute(feature, query));
    }

    [Fact]
    public void CommandRoute_UsesKebabFeatureName()
    {
        var feature = new FeatureModel { Name = "Invoices" };
        var command = new CommandModel { Name = "CreateInvoice" };

        Assert.Equal("/api/invoices", RouteInference.CommandRoute(feature, command));
    }

    [Fact]
    public void QueryRoute_ZeroParams_UsesCollectionRoute()
    {
        var feature = new FeatureModel { Name = "Invoices" };
        var query = new QueryModel { Name = "GetAllInvoices", Params = new List<ParamModel>() };

        Assert.Equal("/api/invoices", RouteInference.QueryRoute(feature, query));
    }

    [Theory]
    [InlineData(CommandKind.Update)]
    [InlineData(CommandKind.Delete)]
    public void CommandRoute_UpdateOrDelete_AppendsIdRouteSegment(CommandKind kind)
    {
        var feature = new FeatureModel { Name = "Invoices" };
        var command = new CommandModel { Name = "UpdateInvoice", Kind = kind };

        Assert.Equal("/api/invoices/{id}", RouteInference.CommandRoute(feature, command));
    }
}
