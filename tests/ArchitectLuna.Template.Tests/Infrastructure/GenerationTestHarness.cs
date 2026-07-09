using ArchitectLuna.Adapters.MediatR;
using ArchitectLuna.Adapters.Wolverine;
using ArchitectLuna.Core.Generation;
using ArchitectLuna.Core.Model;
using ArchitectLuna.Persistence.EfCore;
using ArchitectLuna.Persistence.InMemory;
using ArchitectLuna.Persistence.Marten;

namespace ArchitectLuna.Template.Tests.Infrastructure;

/// <summary>
/// In-memory generation harness for the template/snapshot tier: adapters return
/// <see cref="GeneratedFile"/> records and every foundation builder is a pure function, so the
/// full generated output of a model can be asserted on without any file or process I/O — this
/// whole tier runs in milliseconds, unlike the E2E tier which does real `dotnet build`s.
/// </summary>
public static class GenerationTestHarness
{
    public const string SolutionName = "BillingService";

    public static GenerationContext VerticalSliceContext() =>
        GenerationContext.ForVerticalSlice(SolutionName, $"src/{SolutionName}.Api");

    public static GenerationContext CleanArchitectureContext() =>
        GenerationContext.ForCleanArchitecture(
            SolutionName,
            $"src/{SolutionName}.Api",
            $"src/{SolutionName}.Application",
            $"src/{SolutionName}.Domain",
            $"src/{SolutionName}.Infrastructure",
            $"src/{SolutionName}.Contracts");

    public static IPersistenceGenerator Persistence(string name) => name switch
    {
        "none" => new NullPersistenceGenerator(),
        "in-memory" => new InMemoryPersistenceGenerator(),
        "efcore-postgres" => new EfCorePersistenceGenerator(EfCoreProviderKind.Postgres),
        "efcore-sqlserver" => new EfCorePersistenceGenerator(EfCoreProviderKind.SqlServer),
        "marten" => new MartenPersistenceGenerator(),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    public static IFrameworkAdapter Adapter(string name, IPersistenceGenerator persistence) => name switch
    {
        "mediatr" => new MediatRAdapter(persistence),
        "wolverine" => new WolverineAdapter(persistence),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    /// <summary>The requirement docs' canonical example: an Invoices feature with an Invoice entity and its synthesized CRUD.</summary>
    public static FeatureModel InvoiceFeature()
    {
        var entity = new EntityModel
        {
            Name = "Invoice",
            Fields = new List<FieldModel>
            {
                new() { Name = "CustomerId", Type = "Guid" },
                new() { Name = "AmountCents", Type = "long", Rules = { "GreaterThan(0)" } },
                new() { Name = "Currency", Type = "string", Rules = { "MaximumLength(3)" } },
            },
        };

        var (commands, queries) = CrudSynthesizer.SynthesizeCrud(entity);
        var feature = new FeatureModel { Name = "Invoices", Entities = { entity } };
        feature.Commands.AddRange(commands);
        feature.Queries.AddRange(queries);
        return feature;
    }

    /// <summary>Everything one `generate` run would write for the feature: entity persistence + all slices + solution persistence.</summary>
    public static IReadOnlyList<GeneratedFile> GenerateFeature(
        GenerationContext context, string adapterName, string persistenceName, FeatureModel feature)
    {
        var persistence = Persistence(persistenceName);
        var adapter = Adapter(adapterName, persistence);

        var files = new List<GeneratedFile>();
        foreach (var entity in feature.Entities)
        {
            files.AddRange(persistence.GenerateEntityPersistence(context, feature, entity));
        }

        foreach (var command in feature.Commands)
        {
            files.AddRange(adapter.GenerateCommand(context, feature, command));
        }

        foreach (var query in feature.Queries)
        {
            files.AddRange(adapter.GenerateQuery(context, feature, query));
        }

        var references = feature.Entities.Select(e => new EntityReference(feature, e)).ToList();
        files.AddRange(persistence.GenerateSolutionPersistence(context, references));
        return files;
    }

    public static string ContentOf(IReadOnlyList<GeneratedFile> files, string relativePath)
    {
        var file = files.SingleOrDefault(f => f.RelativePath == relativePath);
        if (file is null)
        {
            var available = string.Join("\n  ", files.Select(f => f.RelativePath).OrderBy(p => p));
            throw new Xunit.Sdk.XunitException($"Expected generated file '{relativePath}' but it was not produced. Generated files:\n  {available}");
        }

        return file.Content;
    }
}
