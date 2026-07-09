using ArchitectLuna.Core.Model;
using ArchitectLuna.Core.Yaml;
using Xunit;

namespace ArchitectLuna.Core.Tests;

public sealed class ModelSerializerTests
{
    [Fact]
    public void SerializeThenDeserialize_RoundTripsAllFields()
    {
        var model = new ArchitectModel
        {
            SolutionName = "BillingService",
            Namespace = "BillingService",
            Adapter = "mediatr",
            Layout = SolutionLayout.CleanArchitecture,
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
                                new() { Name = "AmountCents", Type = "long", Rules = new List<string> { "GreaterThan(0)" } },
                            },
                        },
                    },
                    Queries = new List<QueryModel>
                    {
                        new() { Name = "GetInvoiceById", Params = new List<ParamModel> { new() { Name = "Id", Type = "Guid" } } },
                    },
                },
            },
        };

        var yaml = ModelSerializer.Serialize(model);
        var roundTripped = ModelSerializer.Deserialize(yaml);

        Assert.Equal(model.SolutionName, roundTripped.SolutionName);
        Assert.Equal(model.Adapter, roundTripped.Adapter);
        Assert.Equal(model.Layout, roundTripped.Layout);
        Assert.Single(roundTripped.Features);
        Assert.Equal("CreateInvoice", roundTripped.Features[0].Commands[0].Name);
        Assert.Equal("GreaterThan(0)", roundTripped.Features[0].Commands[0].Fields[0].Rules[0]);
        Assert.Equal("GetInvoiceById", roundTripped.Features[0].Queries[0].Name);
    }

    [Fact]
    public void Database_DefaultsToManualWhenOmitted()
    {
        var model = new ArchitectModel
        {
            SolutionName = "BillingService",
            Namespace = "BillingService",
            Adapter = "mediatr",
            Layout = SolutionLayout.CleanArchitecture,
        };

        var roundTripped = ModelSerializer.Deserialize(ModelSerializer.Serialize(model));

        Assert.Equal(DatabaseApplyMode.Manual, roundTripped.Database.ApplyMode);
    }

    [Fact]
    public void Database_ApplyMode_RoundTrips()
    {
        var model = new ArchitectModel
        {
            SolutionName = "BillingService",
            Namespace = "BillingService",
            Adapter = "mediatr",
            Layout = SolutionLayout.CleanArchitecture,
            Database = new DatabaseSettings { ApplyMode = DatabaseApplyMode.OnStartup },
        };

        var roundTripped = ModelSerializer.Deserialize(ModelSerializer.Serialize(model));

        Assert.Equal(DatabaseApplyMode.OnStartup, roundTripped.Database.ApplyMode);
    }
}
