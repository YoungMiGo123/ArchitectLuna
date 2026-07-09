using System.Text;
using ArchitectLuna.Core.Generation;
using ArchitectLuna.Core.Model;

namespace ArchitectLuna.Persistence.Marten;

/// <summary>
/// Marten persistence for entity-backed CRUD handlers. Marten is a Postgres-native document
/// database, so there is no separate mapping/configuration step and no DbContext-equivalent
/// aggregate file — each entity is just a plain POCO document class with a Guid "Id" property,
/// which Marten auto-detects as the document identity. Document classes go under
/// <see cref="GenerationContext.Domain"/>/Documents; for vertical slice that's a "Persistence"
/// subfolder of the API project (byte-identical to pre-multi-root output), for Clean Architecture
/// it's the Domain project itself.
///
/// Handler bodies get an <c>IDocumentSession</c> injected (MediatR via constructor, Wolverine via
/// an extra static-method parameter) and do straightforward Store/Load/Delete/Query calls — no
/// repository layer, matching the tool's "simplistic" generated-code philosophy. IDocumentSession
/// also implements IQuerySession, so the same single dependency covers both commands and queries.
/// It's already an interface owned by Marten, so unlike EF Core's DbContext there's no need to
/// generate an abstraction for Clean Architecture's Application→Infrastructure dependency rule.
/// </summary>
public sealed class MartenPersistenceGenerator : IPersistenceGenerator
{
    public string Name => "marten";

    public IReadOnlyList<string> RequiredPackages { get; } = new[] { "Marten" };

    // Handler bodies (session.Store/Load/Query/SaveChangesAsync, IDocumentSession) live in the
    // Application project, so it needs the same single package Infrastructure does — Marten has no
    // separate "abstractions-only" package the way EF Core does.
    public IReadOnlyList<string> ApplicationRequiredPackages { get; } = new[] { "Marten" };

    public IReadOnlyList<string> ProgramCsUsings { get; } = new[] { "Marten" };

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

        var body = query.IsCollection
            ? RenderGetAllBody(entity, resultName)
            : RenderGetByIdBody(entity, resultName);

        return new HandlerBinding(body, "IDocumentSession", "session", HandlerUsings(context));
    }

    public IReadOnlyList<string> BuildProgramCsRegistration(GenerationContext context)
    {
        return new[]
        {
            "builder.Services.AddMarten(options => options.Connection(builder.Configuration.GetConnectionString(\"Default\")!));",
        };
    }

    private static List<string> HandlerUsings(GenerationContext context) => new()
    {
        $"{context.Domain.RootNamespace}.Documents",
        "Marten",
    };

    private static string RenderDocumentClass(GenerationContext context, EntityModel entity)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"namespace {context.Domain.RootNamespace}.Documents;");
        sb.AppendLine();
        sb.AppendLine($"public sealed class {entity.Name}");
        sb.AppendLine("{");
        sb.AppendLine("    public Guid Id { get; set; }");
        foreach (var field in entity.Fields)
        {
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
        sb.Append($"return new {resultName}(entity.Id);");
        return sb.ToString();
    }

    private static string RenderUpdateBody(EntityModel entity, string resultName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"var entity = await session.LoadAsync<{entity.Name}>(message.Id, cancellationToken)");
        sb.AppendLine($"    ?? throw new KeyNotFoundException($\"{entity.Name} '{{message.Id}}' was not found.\");");
        foreach (var field in entity.Fields)
        {
            sb.AppendLine($"entity.{field.Name} = message.{field.Name};");
        }

        sb.AppendLine("session.Store(entity);");
        sb.AppendLine("await session.SaveChangesAsync(cancellationToken);");
        sb.Append($"return new {resultName}(entity.Id);");
        return sb.ToString();
    }

    private static string RenderDeleteBody(EntityModel entity, string resultName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"session.Delete<{entity.Name}>(message.Id);");
        sb.AppendLine("await session.SaveChangesAsync(cancellationToken);");
        sb.Append($"return new {resultName}(message.Id);");
        return sb.ToString();
    }

    private static string RenderGetByIdBody(EntityModel entity, string resultName)
    {
        var args = string.Join(", ", new[] { "entity.Id" }.Concat(entity.Fields.Select(f => $"entity.{f.Name}")));
        var sb = new StringBuilder();
        sb.AppendLine($"var entity = await session.LoadAsync<{entity.Name}>(message.Id, cancellationToken)");
        sb.AppendLine($"    ?? throw new KeyNotFoundException($\"{entity.Name} '{{message.Id}}' was not found.\");");
        sb.Append($"return new {resultName}({args});");
        return sb.ToString();
    }

    private static string RenderGetAllBody(EntityModel entity, string resultName)
    {
        var args = string.Join(", ", new[] { "entity.Id" }.Concat(entity.Fields.Select(f => $"entity.{f.Name}")));
        var sb = new StringBuilder();
        sb.AppendLine($"var entities = await session.Query<{entity.Name}>().ToListAsync(cancellationToken);");
        sb.Append($"return entities.Select(entity => new {resultName}({args})).ToList();");
        return sb.ToString();
    }
}
