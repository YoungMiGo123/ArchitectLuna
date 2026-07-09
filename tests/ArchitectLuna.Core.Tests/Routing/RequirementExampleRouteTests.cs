using ArchitectLuna.Core.Model;
using ArchitectLuna.Core.Naming;
using Xunit;

namespace ArchitectLuna.Core.Tests.Routing;

/// <summary>
/// The canonical examples from docs/requirements/002-testing-layer.md, verified end to end
/// through CRUD synthesis + route inference:
///   CreateInvoice   -> POST /api/invoices
///   GetInvoiceById  -> GET  /api/invoices/{id}
///   GetAllInvoices  -> GET  /api/invoices
/// </summary>
public sealed class RequirementExampleRouteTests
{
    private static (FeatureModel Feature, List<CommandModel> Commands, List<QueryModel> Queries) SynthesizedInvoiceFeature()
    {
        var entity = new EntityModel
        {
            Name = "Invoice",
            Fields = new List<FieldModel> { new() { Name = "AmountCents", Type = "long" } },
        };
        var (commands, queries) = CrudSynthesizer.SynthesizeCrud(entity);
        var feature = new FeatureModel { Name = "Invoices", Entities = { entity } };
        feature.Commands.AddRange(commands);
        feature.Queries.AddRange(queries);
        return (feature, commands, queries);
    }

    [Fact]
    public void CreateInvoice_RoutesToCollectionPost()
    {
        var (feature, commands, _) = SynthesizedInvoiceFeature();
        var create = commands.Single(c => c.Name == "CreateInvoice");

        Assert.Equal(CommandKind.Create, create.Kind);
        Assert.Equal("/api/invoices", RouteInference.CommandRoute(feature, create));
    }

    [Fact]
    public void GetInvoiceById_RouteIncludesId()
    {
        var (feature, _, queries) = SynthesizedInvoiceFeature();
        var getById = queries.Single(q => q.Name == "GetInvoiceById");

        Assert.Equal("/api/invoices/{id}", RouteInference.QueryRoute(feature, getById));
    }

    [Fact]
    public void GetAllInvoices_RouteIsCollectionRoute()
    {
        var (feature, _, queries) = SynthesizedInvoiceFeature();
        var getAll = queries.Single(q => q.Name == "GetAllInvoices");

        Assert.True(getAll.IsCollection);
        Assert.Equal("/api/invoices", RouteInference.QueryRoute(feature, getAll));
    }

    [Fact]
    public void UpdateAndDeleteInvoice_RoutesIncludeId()
    {
        var (feature, commands, _) = SynthesizedInvoiceFeature();

        Assert.Equal("/api/invoices/{id}", RouteInference.CommandRoute(feature, commands.Single(c => c.Name == "UpdateInvoice")));
        Assert.Equal("/api/invoices/{id}", RouteInference.CommandRoute(feature, commands.Single(c => c.Name == "DeleteInvoice")));
    }
}
