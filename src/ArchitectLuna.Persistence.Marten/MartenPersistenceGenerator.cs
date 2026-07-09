using System.Text;
using ArchitectLuna.Core.Generation;
using ArchitectLuna.Core.Model;

namespace ArchitectLuna.Persistence.Marten;

/// <summary>
/// Marten persistence for entity-backed CRUD handlers. Marten is a Postgres-native document
/// database, so there is no separate mapping/configuration step and no DbContext-equivalent
/// aggregate file — each entity is just a document class (inheriting the scaffolded
/// <c>BaseEntity</c>, whose Guid "Id" property Marten auto-detects as the document identity).
/// Document classes go under <see cref="GenerationContext.Domain"/>/Documents; for vertical slice
/// that's a "Persistence" subfolder of the API project (byte-identical to pre-multi-root output),
/// for Clean Architecture it's the Domain project itself.
///
/// Handler bodies get an <c>IDocumentSession</c> injected (MediatR via constructor, Wolverine via
/// an extra static-method parameter), do straightforward Store/Load/Delete/Query calls — no
/// repository layer, matching the tool's "simplistic" generated-code philosophy — and return
/// <c>Result&lt;T&gt;</c> outcomes (NotFound as an explicit failure, never an exception for a
/// normal business outcome). IDocumentSession also implements IQuerySession, so the same single
/// dependency covers both commands and queries. It's already an interface owned by Marten, so
/// unlike EF Core's DbContext there's no need to generate an abstraction for Clean Architecture's
/// Application→Infrastructure dependency rule.
/// </summary>
public sealed class MartenPersistenceGenerator : IPersistenceGenerator
{
    public string Name => "marten";

    // Marten covers the store; the HealthChecks abstractions are needed because — under Clean
    // Architecture — the generated MartenHealthCheck (IHealthCheck) lives in the Infrastructure
    // class library, which doesn't get them from the web framework. (Marten brings the hosting
    // abstractions its own ApplyAllDatabaseChangesOnStartup hosted service needs.)
    public IReadOnlyList<string> RequiredPackages { get; } = new[] { "Marten", "Microsoft.Extensions.Diagnostics.HealthChecks" };

    // Handler bodies (session.Store/Load/Query/SaveChangesAsync, IDocumentSession) live in the
    // Application project, so it needs the same single package Infrastructure does — Marten has no
    // separate "abstractions-only" package the way EF Core does.
    public IReadOnlyList<string> ApplicationRequiredPackages { get; } = new[] { "Marten" };

    public IReadOnlyList<GeneratedFile> GenerateEntityPersistence(GenerationContext context, FeatureModel feature, EntityModel entity)
    {
        var documentPath = $"{context.Domain.ProjectRoot}/Documents/{entity.Name}.cs";
        return new[] { new GeneratedFile(documentPath, RenderDocumentClass(context, entity)) };
    }

    public IReadOnlyList<GeneratedFile> GenerateSolutionPersistence(GenerationContext context, IReadOnlyList<EntityReference> entities) =>
        new[]
        {
            new GeneratedFile($"{context.Infrastructure.ProjectRoot}/MartenHealthCheck.cs", RenderMartenHealthCheck(context)),
            new GeneratedFile($"{context.Infrastructure.ProjectRoot}/PersistenceRegistration.cs", RenderAddPersistence(context, entities)),
        };

    public HandlerBinding BindCommandHandler(GenerationContext context, FeatureModel feature, EntityModel entity, CommandModel command)
    {
        var resultName = $"{command.Name}Result";

        var body = command.Kind switch
        {
            CommandKind.Create => RenderCreateBody(entity, resultName),
            CommandKind.Update => RenderUpdateBody(entity, resultName),
            CommandKind.Delete => RenderDeleteBody(entity, resultName),
            _ => "throw new NotImplementedException();",
        };

        return new HandlerBinding(body, "IDocumentSession", "session", HandlerUsings(context));
    }

    public HandlerBinding BindQueryHandler(GenerationContext context, FeatureModel feature, EntityModel entity, QueryModel query)
    {
        var resultName = $"{query.Name}Result";

        var body = query.IsPaged
            ? RenderGetAllPagedBody(entity, resultName)
            : query.IsCollection
                ? RenderGetAllBody(entity, resultName)
                : RenderGetByIdBody(entity, resultName);

        return new HandlerBinding(body, "IDocumentSession", "session", HandlerUsings(context));
    }

    private static string RenderAddPersistence(GenerationContext context, IReadOnlyList<EntityReference> entities)
    {
        var usings = new List<string>
        {
            "Marten",
            "Microsoft.Extensions.Configuration",
            "Microsoft.Extensions.DependencyInjection",
        };
        if (entities.Count > 0)
        {
            usings.Add($"{context.Domain.RootNamespace}.Documents");
        }

        var registrations = entities
            .DistinctBy(e => e.Entity.Name)
            .Select(e => $"            options.RegisterDocumentType<{e.Entity.Name}>();")
            .ToList();
        var registrationBlock = registrations.Count > 0 ? "\n" + string.Join("\n", registrations) : string.Empty;

        var usingBlock = string.Join("\n", usings.Distinct().Select(u => $"using {u};"));

        // RegisterDocumentType makes Marten aware of every generated document up front;
        // ApplyAllDatabaseChangesOnStartup creates/updates each document's table at startup, so a
        // freshly generated solution has its schema without any manual migration step.
        return $$"""
        {{usingBlock}}

        namespace {{context.Infrastructure.RootNamespace}};

        /// <summary>
        /// Everything Marten persistence registers: the document store (with every generated
        /// document type registered), startup schema application, and a database readiness health
        /// check. Regenerated on every `generate` so newly added documents are registered.
        /// </summary>
        public static class PersistenceRegistration
        {
            public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
            {
                services.AddMarten(options =>
                {
                    options.Connection(configuration.GetConnectionString("Default")!);{{registrationBlock}}
                }).ApplyAllDatabaseChangesOnStartup();

                services.AddHealthChecks().AddCheck<MartenHealthCheck>("database", tags: new[] { "ready" });
                return services;
            }
        }
        """;
    }

    private static string RenderMartenHealthCheck(GenerationContext context) =>
        $$"""
        using Marten;
        using Microsoft.Extensions.Diagnostics.HealthChecks;

        namespace {{context.Infrastructure.RootNamespace}};

        /// <summary>Readiness check: reports Unhealthy until the Marten/Postgres store is reachable.</summary>
        public sealed class MartenHealthCheck : IHealthCheck
        {
            private readonly IDocumentStore _store;

            public MartenHealthCheck(IDocumentStore store)
            {
                _store = store;
            }

            public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                try
                {
                    await using var session = _store.QuerySession();
                    await session.QueryAsync<int>("select 1", cancellationToken);
                    return HealthCheckResult.Healthy();
                }
                catch (Exception ex)
                {
                    return HealthCheckResult.Unhealthy("The Marten store is not reachable.", ex);
                }
            }
        }
        """;

    private static List<string> HandlerUsings(GenerationContext context) => new()
    {
        $"{context.Application.RootNamespace}.Common.Results",
        $"{context.Domain.RootNamespace}.Documents",
        "Marten",
    };

    private static string RenderDocumentClass(GenerationContext context, EntityModel entity)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"using {context.Domain.RootNamespace}.Common;");
        sb.AppendLine();
        sb.AppendLine($"namespace {context.Domain.RootNamespace}.Documents;");
        sb.AppendLine();
        sb.AppendLine($"public sealed class {entity.Name} : BaseEntity");
        sb.AppendLine("{");
        var first = true;
        foreach (var field in entity.Fields)
        {
            if (!first)
            {
                sb.AppendLine();
            }

            first = false;
            sb.AppendLine($"    public {field.Type} {field.Name} {{ get; set; }}{DefaultInitializer(field.Type)}");
        }

        sb.Append('}');
        return sb.ToString();
    }

    private static string DefaultInitializer(string type) => type == "string" ? " = string.Empty;" : string.Empty;

    private static string RenderCreateBody(EntityModel entity, string resultName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"var entity = new {entity.Name}");
        sb.AppendLine("{");
        sb.AppendLine("    Id = Guid.NewGuid(),");
        foreach (var field in entity.Fields)
        {
            sb.AppendLine($"    {field.Name} = message.{field.Name},");
        }

        sb.AppendLine("};");
        sb.AppendLine("session.Store(entity);");
        sb.AppendLine("await session.SaveChangesAsync(cancellationToken);");
        sb.Append($"return Result<{resultName}>.Success(new {resultName}(entity.Id));");
        return sb.ToString();
    }

    private static string RenderUpdateBody(EntityModel entity, string resultName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"var entity = await session.LoadAsync<{entity.Name}>(message.Id, cancellationToken);");
        sb.AppendLine("if (entity is null)");
        sb.AppendLine("{");
        sb.AppendLine($"    return Result<{resultName}>.Failure(Error.NotFound($\"{entity.Name} '{{message.Id}}' was not found.\"));");
        sb.AppendLine("}");
        sb.AppendLine();
        foreach (var field in entity.Fields)
        {
            sb.AppendLine($"entity.{field.Name} = message.{field.Name};");
        }

        sb.AppendLine("session.Store(entity);");
        sb.AppendLine("await session.SaveChangesAsync(cancellationToken);");
        sb.Append($"return Result<{resultName}>.Success(new {resultName}(entity.Id));");
        return sb.ToString();
    }

    private static string RenderDeleteBody(EntityModel entity, string resultName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"var entity = await session.LoadAsync<{entity.Name}>(message.Id, cancellationToken);");
        sb.AppendLine("if (entity is null)");
        sb.AppendLine("{");
        sb.AppendLine($"    return Result<{resultName}>.Failure(Error.NotFound($\"{entity.Name} '{{message.Id}}' was not found.\"));");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine($"session.Delete<{entity.Name}>(message.Id);");
        sb.AppendLine("await session.SaveChangesAsync(cancellationToken);");
        sb.Append($"return Result<{resultName}>.Success(new {resultName}(message.Id));");
        return sb.ToString();
    }

    private static string RenderGetByIdBody(EntityModel entity, string resultName)
    {
        var args = string.Join(", ", new[] { "entity.Id" }.Concat(entity.Fields.Select(f => $"entity.{f.Name}")));
        var sb = new StringBuilder();
        sb.AppendLine($"var entity = await session.LoadAsync<{entity.Name}>(message.Id, cancellationToken);");
        sb.AppendLine("if (entity is null)");
        sb.AppendLine("{");
        sb.AppendLine($"    return Result<{resultName}>.Failure(Error.NotFound($\"{entity.Name} '{{message.Id}}' was not found.\"));");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.Append($"return Result<{resultName}>.Success(new {resultName}({args}));");
        return sb.ToString();
    }

    private static string RenderGetAllBody(EntityModel entity, string resultName)
    {
        var args = string.Join(", ", new[] { "entity.Id" }.Concat(entity.Fields.Select(f => $"entity.{f.Name}")));
        var sb = new StringBuilder();
        sb.AppendLine($"var entities = await session.Query<{entity.Name}>().ToListAsync(cancellationToken);");
        sb.Append($"return Result<IReadOnlyList<{resultName}>>.Success(entities.Select(entity => new {resultName}({args})).ToList());");
        return sb.ToString();
    }

    private static string RenderGetAllPagedBody(EntityModel entity, string resultName)
    {
        var args = string.Join(", ", new[] { "entity.Id" }.Concat(entity.Fields.Select(f => $"entity.{f.Name}")));
        var sb = new StringBuilder();
        sb.AppendLine("var page = message.Page <= 0 ? 1 : message.Page;");
        sb.AppendLine("var pageSize = message.PageSize <= 0 ? 20 : Math.Min(message.PageSize, 100);");
        sb.AppendLine($"var totalCount = await session.Query<{entity.Name}>().CountAsync(cancellationToken);");
        sb.AppendLine($"var entities = await session.Query<{entity.Name}>().OrderBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);");
        sb.AppendLine($"var items = entities.Select(entity => new {resultName}({args})).ToList();");
        sb.Append($"return Result<PagedResult<{resultName}>>.Success(new PagedResult<{resultName}>(items, page, pageSize, (long)totalCount));");
        return sb.ToString();
    }
}
