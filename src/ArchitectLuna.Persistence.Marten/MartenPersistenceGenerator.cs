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

    public IReadOnlyList<string> RequiredPackages { get; } = new[] { "Marten" };

    // Handler bodies (session.Store/Load/Query/SaveChangesAsync, IDocumentSession) live in the
    // Application project, so it needs the same single package Infrastructure does — Marten has no
    // separate "abstractions-only" package the way EF Core does.
    public IReadOnlyList<string> ApplicationRequiredPackages { get; } = new[] { "Marten" };

    public IReadOnlyList<string> ServiceRegistrationUsings { get; } = new[] { "Marten" };

    public IReadOnlyList<string> BuildStartupApplyLines(GenerationContext context) => new[]
    {
        "using (var scope = app.Services.CreateScope())",
        "{",
        "    await scope.ServiceProvider.GetRequiredService<IDocumentStore>().Storage.ApplyAllConfiguredChangesToDatabaseAsync();",
        "}",
    };

    public IReadOnlyList<string> StartupApplyUsings { get; } = new[] { "Microsoft.Extensions.DependencyInjection", "Marten" };

    public IReadOnlyList<GeneratedFile> GenerateEntityPersistence(GenerationContext context, FeatureModel feature, EntityModel entity)
    {
        var documentPath = $"{context.Domain.ProjectRoot}/Documents/{entity.Name}.cs";
        return new[] { new GeneratedFile(documentPath, RenderDocumentClass(context, entity)) };
    }

    public IReadOnlyList<GeneratedFile> GenerateSolutionPersistence(GenerationContext context, IReadOnlyList<EntityReference> entities) =>
        Array.Empty<GeneratedFile>();

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
            ? RenderPagedGetAllBody(entity, resultName)
            : query.IsCollection
                ? RenderGetAllBody(entity, resultName)
                : RenderGetByIdBody(entity, resultName);

        return new HandlerBinding(body, "IDocumentSession", "session", HandlerUsings(context));
    }

    public IReadOnlyList<string> BuildServiceRegistration(GenerationContext context)
    {
        return new[]
        {
            "services.AddMarten(options => options.Connection(configuration.GetConnectionString(\"Default\")!));",
        };
    }

    /// <summary>
    /// docs/requirements/003-improvements.md §11: manual/on-generate leave Marten's default
    /// (only creates schema objects that don't exist yet); on-startup additionally allows Marten
    /// to update existing schema objects, paired with the startup-time apply call in
    /// <see cref="BuildStartupApplyLines"/>.
    /// </summary>
    public IReadOnlyList<string> BuildServiceRegistration(GenerationContext context, DatabaseApplyMode applyMode)
    {
        // Fully qualified: StoreOptions.AutoCreateSchemaObjects is JasperFx.AutoCreate (moved out
        // of the Marten package itself in Marten 9.x), not Marten.AutoCreate — qualifying avoids
        // needing a "using JasperFx;" the caller may not otherwise have any reason to add.
        var autoCreate = applyMode == DatabaseApplyMode.OnStartup ? "JasperFx.AutoCreate.All" : "JasperFx.AutoCreate.CreateOrUpdate";
        return new[]
        {
            "services.AddMarten(options =>",
            "{",
            "    options.Connection(configuration.GetConnectionString(\"Default\")!);",
            $"    options.AutoCreateSchemaObjects = {autoCreate};",
            "});",
        };
    }

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

    private static string RenderPagedGetAllBody(EntityModel entity, string resultName)
    {
        var args = string.Join(", ", new[] { "entity.Id" }.Concat(entity.Fields.Select(f => $"entity.{f.Name}")));
        var sb = new StringBuilder();
        sb.AppendLine("var page = message.Page <= 0 ? 1 : message.Page;");
        sb.AppendLine("var pageSize = message.PageSize <= 0 ? 20 : message.PageSize;");
        sb.AppendLine($"var totalCount = (long)await session.Query<{entity.Name}>().CountAsync(cancellationToken);");
        sb.AppendLine($"var entities = await session.Query<{entity.Name}>().OrderBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);");
        sb.Append($"return Result<PagedResult<{resultName}>>.Success(new PagedResult<{resultName}>(entities.Select(entity => new {resultName}({args})).ToList(), page, pageSize, totalCount));");
        return sb.ToString();
    }
}
