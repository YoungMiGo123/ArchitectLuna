using ArchitectLuna.Core.Editing;
using ArchitectLuna.Core.Model;
using Xunit;

namespace ArchitectLuna.Core.Tests.GenerationOrdering;

/// <summary>
/// The ordering rules from docs/requirements/001-implementation-architecture.md §Ordering Rules,
/// enforced by <see cref="ModelEditor"/>: features before entities, entities before CRUD, bespoke
/// commands/queries allowed without entities, and duplicates rejected without corrupting the
/// model. (Rule 1 — nothing outside a project — lives in WorkspaceLocator and is covered by the
/// CLI-level E2E ordering tests.)
/// </summary>
public sealed class ModelEditorTests
{
    private static ArchitectModel EmptyModel() => new()
    {
        SolutionName = "TestApi",
        Namespace = "TestApi",
        Adapter = "mediatr",
        Features = new List<FeatureModel>(),
    };

    private static EntityModel Invoice() => new()
    {
        Name = "Invoice",
        Fields = new List<FieldModel>
        {
            new() { Name = "CustomerId", Type = "Guid" },
            new() { Name = "AmountCents", Type = "long" },
        },
    };

    [Fact]
    public void AddFeature_AddsFeature()
    {
        var model = EmptyModel();

        var result = ModelEditor.AddFeature(model, "Invoices");

        Assert.True(result.Success);
        Assert.Single(model.Features, f => f.Name == "Invoices");
    }

    [Fact]
    public void AddFeature_Duplicate_FailsWithoutCorruptingModel()
    {
        var model = EmptyModel();
        ModelEditor.AddFeature(model, "Invoices");

        var result = ModelEditor.AddFeature(model, "Invoices");

        Assert.False(result.Success);
        Assert.Contains("already exists", result.Error);
        Assert.Single(model.Features);
    }

    [Fact]
    public void AddEntity_MissingFeature_FailsAndPointsAtAddFeature()
    {
        var model = EmptyModel();

        var result = ModelEditor.AddEntity(model, "Invoices", Invoice());

        Assert.False(result.Success);
        Assert.Contains("add feature Invoices", result.Error);
        Assert.Empty(model.Features);
    }

    [Fact]
    public void AddEntity_SynthesizesFullCrud()
    {
        var model = EmptyModel();
        ModelEditor.AddFeature(model, "Invoices");

        var result = ModelEditor.AddEntity(model, "Invoices", Invoice());

        Assert.True(result.Success);
        Assert.Equal(
            new[] { "CreateInvoice", "UpdateInvoice", "DeleteInvoice", "GetInvoiceById", "GetAllInvoices" },
            result.AddedOperations);

        var feature = Assert.Single(model.Features);
        Assert.Single(feature.Entities);
        Assert.Equal(3, feature.Commands.Count);
        Assert.Equal(2, feature.Queries.Count);
    }

    [Fact]
    public void AddEntity_Duplicate_FailsWithoutCorruptingModel()
    {
        var model = EmptyModel();
        ModelEditor.AddFeature(model, "Invoices");
        ModelEditor.AddEntity(model, "Invoices", Invoice());

        var result = ModelEditor.AddEntity(model, "Invoices", Invoice());

        Assert.False(result.Success);
        Assert.Contains("already exists", result.Error);

        var feature = Assert.Single(model.Features);
        Assert.Single(feature.Entities);
        Assert.Equal(3, feature.Commands.Count);
        Assert.Equal(2, feature.Queries.Count);
    }

    [Fact]
    public void AddEntity_CrudNameCollision_FailsWithoutPartialMutation()
    {
        var model = EmptyModel();
        ModelEditor.AddFeature(model, "Invoices");
        ModelEditor.AddCommand(model, "Invoices", "CreateInvoice", CommandKind.Create, new List<FieldModel>());

        var result = ModelEditor.AddEntity(model, "Invoices", Invoice());

        Assert.False(result.Success);
        Assert.Contains("CreateInvoice", result.Error);

        var feature = Assert.Single(model.Features);
        Assert.Empty(feature.Entities);
        Assert.Single(feature.Commands);
        Assert.Empty(feature.Queries);
    }

    [Fact]
    public void AddCrud_MissingFeature_Fails()
    {
        var model = EmptyModel();

        var result = ModelEditor.AddCrud(model, "Invoices", "Invoice");

        Assert.False(result.Success);
        Assert.Contains("add feature Invoices", result.Error);
    }

    [Fact]
    public void AddCrud_MissingEntity_FailsAndTellsUserToCreateEntityFirst()
    {
        var model = EmptyModel();
        ModelEditor.AddFeature(model, "Invoices");

        var result = ModelEditor.AddCrud(model, "Invoices", "Invoice");

        Assert.False(result.Success);
        Assert.Contains("Create the entity first", result.Error);
        Assert.Contains("add entity Invoices Invoice", result.Error);
    }

    [Fact]
    public void AddCrud_ExistingEntity_ResynthesizesOnlyMissingOperations()
    {
        var model = EmptyModel();
        ModelEditor.AddFeature(model, "Invoices");
        ModelEditor.AddEntity(model, "Invoices", Invoice());

        var feature = model.Features.Single();
        feature.Commands.RemoveAll(c => c.Name == "DeleteInvoice");
        feature.Queries.RemoveAll(q => q.Name == "GetAllInvoices");

        var result = ModelEditor.AddCrud(model, "Invoices", "Invoice");

        Assert.True(result.Success);
        Assert.Equal(new[] { "DeleteInvoice", "GetAllInvoices" }, result.AddedOperations);
        Assert.Equal(3, feature.Commands.Count);
        Assert.Equal(2, feature.Queries.Count);
    }

    [Fact]
    public void AddCrud_NothingMissing_SucceedsWithNoAddedOperations()
    {
        var model = EmptyModel();
        ModelEditor.AddFeature(model, "Invoices");
        ModelEditor.AddEntity(model, "Invoices", Invoice());

        var result = ModelEditor.AddCrud(model, "Invoices", "Invoice");

        Assert.True(result.Success);
        Assert.Empty(result.AddedOperations);
    }

    [Fact]
    public void AddCommand_BespokeWithoutEntity_IsAllowed()
    {
        var model = EmptyModel();
        ModelEditor.AddFeature(model, "Reports");

        var result = ModelEditor.AddCommand(model, "Reports", "GenerateMonthlyReport", CommandKind.Create, new List<FieldModel>());

        Assert.True(result.Success);
        Assert.Single(model.Features.Single().Commands, c => c.Name == "GenerateMonthlyReport");
    }

    [Fact]
    public void AddQuery_BespokeWithoutEntity_IsAllowed()
    {
        var model = EmptyModel();
        ModelEditor.AddFeature(model, "Reports");

        var result = ModelEditor.AddQuery(model, "Reports", "GetReportStatus", new List<ParamModel>());

        Assert.True(result.Success);
        Assert.Single(model.Features.Single().Queries, q => q.Name == "GetReportStatus");
    }

    [Fact]
    public void AddCommand_MissingFeature_Fails()
    {
        var model = EmptyModel();

        var result = ModelEditor.AddCommand(model, "Reports", "GenerateMonthlyReport", CommandKind.Create, new List<FieldModel>());

        Assert.False(result.Success);
        Assert.Contains("add feature Reports", result.Error);
    }

    [Fact]
    public void AddCommand_Duplicate_Fails()
    {
        var model = EmptyModel();
        ModelEditor.AddFeature(model, "Reports");
        ModelEditor.AddCommand(model, "Reports", "GenerateMonthlyReport", CommandKind.Create, new List<FieldModel>());

        var result = ModelEditor.AddCommand(model, "Reports", "GenerateMonthlyReport", CommandKind.Create, new List<FieldModel>());

        Assert.False(result.Success);
        Assert.Contains("already exists", result.Error);
        Assert.Single(model.Features.Single().Commands);
    }

    [Fact]
    public void AddCommand_DeleteKindWithFields_Fails()
    {
        var model = EmptyModel();
        ModelEditor.AddFeature(model, "Invoices");

        var result = ModelEditor.AddCommand(model, "Invoices", "PurgeInvoice", CommandKind.Delete, new List<FieldModel>
        {
            new() { Name = "Reason", Type = "string" },
        });

        Assert.False(result.Success);
        Assert.Contains("delete command takes no --field", result.Error);
    }

    [Fact]
    public void AddCommand_UpdateKind_InjectsIdFieldFirst()
    {
        var model = EmptyModel();
        ModelEditor.AddFeature(model, "Invoices");

        var result = ModelEditor.AddCommand(model, "Invoices", "VoidInvoice", CommandKind.Update, new List<FieldModel>
        {
            new() { Name = "Reason", Type = "string" },
        });

        Assert.True(result.Success);
        var command = model.Features.Single().Commands.Single();
        Assert.Equal("Id", command.Fields[0].Name);
        Assert.Equal("Reason", command.Fields[1].Name);
    }

    [Fact]
    public void AddQuery_Duplicate_Fails()
    {
        var model = EmptyModel();
        ModelEditor.AddFeature(model, "Reports");
        ModelEditor.AddQuery(model, "Reports", "GetReportStatus", new List<ParamModel>());

        var result = ModelEditor.AddQuery(model, "Reports", "GetReportStatus", new List<ParamModel>());

        Assert.False(result.Success);
        Assert.Contains("already exists", result.Error);
        Assert.Single(model.Features.Single().Queries);
    }
}
