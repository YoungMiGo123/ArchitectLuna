using ArchitectLuna.Core.Model;
using Xunit;

namespace ArchitectLuna.Core.Tests;

public sealed class CrudSynthesizerTests
{
    private static EntityModel MakeInvoiceEntity() => new()
    {
        Name = "Invoice",
        Fields = new List<FieldModel>
        {
            new() { Name = "CustomerId", Type = "Guid" },
            new() { Name = "AmountCents", Type = "long", Rules = new List<string> { "GreaterThan(0)" } },
        },
    };

    [Fact]
    public void SynthesizeCrud_ProducesCreateUpdateDeleteCommands()
    {
        var (commands, _) = CrudSynthesizer.SynthesizeCrud(MakeInvoiceEntity());

        Assert.Collection(
            commands,
            c => Assert.Equal(("CreateInvoice", CommandKind.Create), (c.Name, c.Kind)),
            c => Assert.Equal(("UpdateInvoice", CommandKind.Update), (c.Name, c.Kind)),
            c => Assert.Equal(("DeleteInvoice", CommandKind.Delete), (c.Name, c.Kind)));
    }

    [Fact]
    public void SynthesizeCrud_CreateCommand_DoesNotIncludeId()
    {
        var (commands, _) = CrudSynthesizer.SynthesizeCrud(MakeInvoiceEntity());
        var create = commands.Single(c => c.Kind == CommandKind.Create);

        Assert.DoesNotContain(create.Fields, f => f.Name == "Id");
        Assert.Equal(2, create.Fields.Count);
    }

    [Fact]
    public void SynthesizeCrud_UpdateCommand_PrependsIdAndPreservesRules()
    {
        var (commands, _) = CrudSynthesizer.SynthesizeCrud(MakeInvoiceEntity());
        var update = commands.Single(c => c.Kind == CommandKind.Update);

        Assert.Equal("Id", update.Fields[0].Name);
        var amountField = update.Fields.Single(f => f.Name == "AmountCents");
        Assert.Contains("GreaterThan(0)", amountField.Rules);
    }

    [Fact]
    public void SynthesizeCrud_DeleteCommand_OnlyHasId()
    {
        var (commands, _) = CrudSynthesizer.SynthesizeCrud(MakeInvoiceEntity());
        var delete = commands.Single(c => c.Kind == CommandKind.Delete);

        Assert.Single(delete.Fields);
        Assert.Equal("Id", delete.Fields[0].Name);
    }

    [Fact]
    public void SynthesizeCrud_ProducesGetByIdAndGetAllQueries()
    {
        var (_, queries) = CrudSynthesizer.SynthesizeCrud(MakeInvoiceEntity());

        var getById = queries.Single(q => q.Name == "GetInvoiceById");
        Assert.Single(getById.Params);
        Assert.False(getById.IsCollection);
        Assert.Equal(3, getById.ResultFields.Count);

        var getAll = queries.Single(q => q.Name == "GetAllInvoices");
        Assert.Empty(getAll.Params);
        Assert.True(getAll.IsCollection);
        Assert.Equal(3, getAll.ResultFields.Count);
    }

    [Fact]
    public void SynthesizeCrud_MutatingReturnedCommandFields_DoesNotAffectSourceEntity()
    {
        var entity = MakeInvoiceEntity();
        var (commands, _) = CrudSynthesizer.SynthesizeCrud(entity);

        commands.Single(c => c.Kind == CommandKind.Create).Fields[0].Rules.Add("NotEmpty()");

        Assert.Empty(entity.Fields[0].Rules);
    }
}
