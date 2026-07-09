using System.Text;
using ArchitectLuna.Core.Generation;
using ArchitectLuna.Core.Model;

namespace ArchitectLuna.Persistence.InMemory;

/// <summary>
/// In-memory persistence for entity-backed CRUD handlers — the zero-setup default. No NuGet
/// package, no connection string, no external process: a single process-lifetime
/// <c>InMemoryStore</c> (generated once per solution, registered as a singleton) backs every
/// entity's Create/Update/Delete/GetById/GetAll via a <c>ConcurrentDictionary</c> keyed by
/// (entity type, id). Data does not survive a restart — that tradeoff is what makes `new api`
/// produce a solution with real, runnable CRUD handlers without requiring Postgres/SQL Server
/// to be running first. Swapping to `efcore-postgres`, `efcore-sqlserver`, or `marten` later is a
/// model-level change (`--persistence`), not a rewrite: the entity shape and handler call sites
/// look the same, only the injected dependency and its backing store differ.
///
/// Entity classes go under <see cref="GenerationContext.Domain"/>/Entities; the store itself goes
/// under <see cref="GenerationContext.Infrastructure"/>, matching EF Core's DbContext placement.
/// For vertical slice, Domain and Infrastructure are the same project (a "Persistence" subfolder
/// of the API project), so this produces the same files/namespaces as before this provider existed
/// alongside multi-root <see cref="GenerationContext"/>. For Clean Architecture, Domain and
/// Infrastructure are genuinely separate projects — Application must not reference Infrastructure
/// directly, so (like EF Core's DbContext) an <c>IInMemoryStore</c> interface is generated in
/// Application when <see cref="GenerationContext.HasSeparateInfrastructure"/> is true, and the
/// concrete <c>InMemoryStore</c> in Infrastructure implements it; handlers depend on the interface
/// instead of the concrete type.
///
/// Handler bodies get the store (or its interface) injected (MediatR via constructor, Wolverine
/// via an extra static-method parameter) and do straightforward Save/Find/Remove/GetAll calls —
/// no repository layer, matching the tool's "simplistic" generated-code philosophy.
/// </summary>
public sealed class InMemoryPersistenceGenerator : IPersistenceGenerator
{
    private const string StoreClassName = "InMemoryStore";
    private const string StoreInterfaceName = "IInMemoryStore";

    public string Name => "in-memory";

    public IReadOnlyList<string> RequiredPackages { get; } = Array.Empty<string>();

    public IReadOnlyList<string> ApplicationRequiredPackages { get; } = Array.Empty<string>();

    public IReadOnlyList<string> ProgramCsUsings { get; } = Array.Empty<string>();

    public IReadOnlyList<GeneratedFile> GenerateEntityPersistence(GenerationContext context, FeatureModel feature, EntityModel entity)
    {
        var entityPath = $"{context.Domain.ProjectRoot}/Entities/{entity.Name}.cs";
        return new[] { new GeneratedFile(entityPath, RenderEntityClass(context, entity)) };
    }

    public IReadOnlyList<GeneratedFile> GenerateSolutionPersistence(GenerationContext context, IReadOnlyList<EntityReference> entities)
    {
        // Always emits the store (it's generic — Save<T>/Find<T>/etc. — so it needs no per-entity
        // content) rather than only once an entity exists: Program.cs references this type
        // unconditionally as soon as in-memory persistence is configured, so the freshly scaffolded
        // solution (before the first `generate`) needs it to already exist and compile.
        var files = new List<GeneratedFile>
        {
            new($"{context.Infrastructure.ProjectRoot}/{StoreClassName}.cs", RenderStoreClass(context)),
        };

        if (context.HasSeparateInfrastructure)
        {
            files.Add(new GeneratedFile(
                $"{context.Application.ProjectRoot}/Persistence/{StoreInterfaceName}.cs",
                RenderStoreInterface(context)));
        }

        return files;
    }

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

        return new HandlerBinding(body, DependencyTypeName(context), "store", HandlerUsings(context));
    }

    public HandlerBinding BindQueryHandler(GenerationContext context, FeatureModel feature, EntityModel entity, QueryModel query)
    {
        var resultName = $"{query.Name}Result";

        var body = query.IsCollection
            ? RenderGetAllBody(entity, resultName)
            : RenderGetByIdBody(entity, resultName);

        return new HandlerBinding(body, DependencyTypeName(context), "store", HandlerUsings(context));
    }

    public IReadOnlyList<string> BuildProgramCsRegistration(GenerationContext context)
    {
        // Fully qualified rather than relying on a "using ...;" being present in Program.cs —
        // keeps this independent of whichever usings the caller composes.
        var qualifiedStoreName = $"{context.Infrastructure.RootNamespace}.{StoreClassName}";
        var lines = new List<string>
        {
            $"builder.Services.AddSingleton<{qualifiedStoreName}>();",
        };

        if (context.HasSeparateInfrastructure)
        {
            var qualifiedInterfaceName = $"{context.Application.RootNamespace}.Persistence.{StoreInterfaceName}";
            lines.Add($"builder.Services.AddSingleton<{qualifiedInterfaceName}>(sp => sp.GetRequiredService<{qualifiedStoreName}>());");
        }

        return lines;
    }

    private static string DependencyTypeName(GenerationContext context) =>
        context.HasSeparateInfrastructure ? StoreInterfaceName : StoreClassName;

    private static List<string> HandlerUsings(GenerationContext context) => new()
    {
        context.HasSeparateInfrastructure ? $"{context.Application.RootNamespace}.Persistence" : context.Infrastructure.RootNamespace,
        $"{context.Domain.RootNamespace}.Entities",
    };

    private static string RenderEntityClass(GenerationContext context, EntityModel entity)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"namespace {context.Domain.RootNamespace}.Entities;");
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

    private static string RenderStoreInterface(GenerationContext context) =>
        $$"""
        namespace {{context.Application.RootNamespace}}.Persistence;

        public interface {{StoreInterfaceName}}
        {
            void Save<T>(Guid id, T entity) where T : notnull;

            T? Find<T>(Guid id) where T : class;

            void Remove<T>(Guid id);

            IReadOnlyList<T> GetAll<T>();
        }
        """;

    private static string RenderStoreClass(GenerationContext context)
    {
        var usings = context.HasSeparateInfrastructure
            ? $"using System.Collections.Concurrent;\nusing {context.Application.RootNamespace}.Persistence;"
            : "using System.Collections.Concurrent;";
        var baseList = context.HasSeparateInfrastructure ? $" : {StoreInterfaceName}" : string.Empty;

        return $$"""
        {{usings}}

        namespace {{context.Infrastructure.RootNamespace}};

        /// <summary>
        /// Process-lifetime store backing the "in-memory" persistence provider. One dictionary
        /// keyed by (entity type, id) covers every entity — no per-entity registration needed in
        /// Program.cs. Registered as a singleton, so data survives across requests but not across
        /// an app restart.
        /// </summary>
        public sealed class {{StoreClassName}}{{baseList}}
        {
            private readonly ConcurrentDictionary<(Type EntityType, Guid Id), object> items = new();

            public void Save<T>(Guid id, T entity) where T : notnull =>
                items[(typeof(T), id)] = entity;

            public T? Find<T>(Guid id) where T : class =>
                items.TryGetValue((typeof(T), id), out var value) ? (T)value : null;

            public void Remove<T>(Guid id) =>
                items.TryRemove((typeof(T), id), out _);

            public IReadOnlyList<T> GetAll<T>() =>
                items.Where(kv => kv.Key.EntityType == typeof(T)).Select(kv => (T)kv.Value).ToList();
        }
        """;
    }

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
        sb.AppendLine("store.Save(entity.Id, entity);");
        sb.Append($"return new {resultName}(entity.Id);");
        return sb.ToString();
    }

    private static string RenderUpdateBody(EntityModel entity, string resultName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"var entity = store.Find<{entity.Name}>(message.Id)");
        sb.AppendLine($"    ?? throw new KeyNotFoundException($\"{entity.Name} '{{message.Id}}' was not found.\");");
        foreach (var field in entity.Fields)
        {
            sb.AppendLine($"entity.{field.Name} = message.{field.Name};");
        }

        sb.AppendLine("store.Save(entity.Id, entity);");
        sb.Append($"return new {resultName}(entity.Id);");
        return sb.ToString();
    }

    private static string RenderDeleteBody(EntityModel entity, string resultName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"store.Remove<{entity.Name}>(message.Id);");
        sb.Append($"return new {resultName}(message.Id);");
        return sb.ToString();
    }

    private static string RenderGetByIdBody(EntityModel entity, string resultName)
    {
        var args = string.Join(", ", new[] { "entity.Id" }.Concat(entity.Fields.Select(f => $"entity.{f.Name}")));
        var sb = new StringBuilder();
        sb.AppendLine($"var entity = store.Find<{entity.Name}>(message.Id)");
        sb.AppendLine($"    ?? throw new KeyNotFoundException($\"{entity.Name} '{{message.Id}}' was not found.\");");
        sb.Append($"return new {resultName}({args});");
        return sb.ToString();
    }

    private static string RenderGetAllBody(EntityModel entity, string resultName)
    {
        var args = string.Join(", ", new[] { "entity.Id" }.Concat(entity.Fields.Select(f => $"entity.{f.Name}")));
        var sb = new StringBuilder();
        sb.AppendLine($"var entities = store.GetAll<{entity.Name}>();");
        sb.Append($"return entities.Select(entity => new {resultName}({args})).ToList();");
        return sb.ToString();
    }
}
