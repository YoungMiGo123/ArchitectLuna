using System.Text;
using ArchitectLuna.Core.Generation;
using ArchitectLuna.Core.Model;
using ArchitectLuna.Core.Naming;

namespace ArchitectLuna.Persistence.EfCore;

/// <summary>
/// EF Core persistence for entity-backed CRUD handlers. Postgres and SQL Server share this one
/// implementation — they differ only in package name and the `UseNpgsql`/`UseSqlServer` call —
/// so <see cref="EfCoreProviderKind"/> parameterizes rather than duplicating the class.
///
/// Generates, per entity, a plain domain class under <see cref="GenerationContext.Domain"/>/Entities
/// and an IEntityTypeConfiguration under <see cref="GenerationContext.Infrastructure"/>/Configurations;
/// once per solution, a DbContext in Infrastructure with one DbSet per entity. For vertical slice,
/// Domain and Infrastructure are the same project (a "Persistence" subfolder of the API project), so
/// this produces exactly the same files/namespaces as before this context became multi-root. For
/// Clean Architecture, Domain and Infrastructure are genuinely separate projects — Application must
/// not reference Infrastructure directly, so an <c>I{Solution}DbContext</c> interface is generated
/// in Application (see <see cref="GenerationContext.HasSeparateInfrastructure"/>) and the concrete
/// DbContext in Infrastructure implements it; handlers depend on the interface instead of the
/// concrete type.
///
/// Handler bodies get the DbContext (or its interface) constructor/method-injected (MediatR via
/// constructor, Wolverine via an extra static-method parameter) and do straightforward
/// Add/Find/Remove/SaveChanges calls — no repository layer, matching the tool's "simplistic"
/// generated-code philosophy.
/// </summary>
public sealed class EfCorePersistenceGenerator : IPersistenceGenerator
{
    private readonly EfCoreProviderKind _kind;

    public EfCorePersistenceGenerator(EfCoreProviderKind kind)
    {
        _kind = kind;
    }

    public string Name => _kind == EfCoreProviderKind.Postgres ? "efcore-postgres" : "efcore-sqlserver";

    public IReadOnlyList<string> RequiredPackages => _kind == EfCoreProviderKind.Postgres
        ? new[] { "Microsoft.EntityFrameworkCore", "Npgsql.EntityFrameworkCore.PostgreSQL" }
        : new[] { "Microsoft.EntityFrameworkCore", "Microsoft.EntityFrameworkCore.SqlServer" };

    // Handler bodies (dbContext.Add/Find/Remove/SaveChangesAsync) and, under Clean Architecture,
    // the I{Solution}DbContext interface's DbSet<T> properties both live in Application — neither
    // needs the database-specific provider package, only EF Core's own abstractions.
    public IReadOnlyList<string> ApplicationRequiredPackages { get; } = new[] { "Microsoft.EntityFrameworkCore" };

    public IReadOnlyList<string> ProgramCsUsings { get; } = new[] { "Microsoft.EntityFrameworkCore" };

    public IReadOnlyList<GeneratedFile> GenerateEntityPersistence(GenerationContext context, FeatureModel feature, EntityModel entity)
    {
        var entityPath = $"{context.Domain.ProjectRoot}/Entities/{entity.Name}.cs";
        var configPath = $"{context.Infrastructure.ProjectRoot}/Configurations/{entity.Name}Configuration.cs";

        return new[]
        {
            new GeneratedFile(entityPath, RenderEntityClass(context, entity)),
            new GeneratedFile(configPath, RenderEntityConfiguration(context, entity)),
        };
    }

    public IReadOnlyList<GeneratedFile> GenerateSolutionPersistence(GenerationContext context, IReadOnlyList<EntityReference> entities)
    {
        // Always emits the DbContext (even with zero DbSets) rather than only once an entity
        // exists: Program.cs references this type unconditionally as soon as EF Core persistence
        // is configured, so the freshly scaffolded solution (before the first `generate`) needs it
        // to already exist and compile.
        var files = new List<GeneratedFile>
        {
            new($"{context.Infrastructure.ProjectRoot}/{DbContextName(context)}.cs", RenderDbContext(context, entities)),
        };

        if (context.HasSeparateInfrastructure)
        {
            files.Add(new GeneratedFile(
                $"{context.Application.ProjectRoot}/Persistence/{DbContextInterfaceName(context)}.cs",
                RenderDbContextInterface(context, entities)));
        }

        return files;
    }

    public HandlerBinding BindCommandHandler(GenerationContext context, FeatureModel feature, EntityModel entity, CommandModel command)
    {
        var resultName = $"{command.Name}Result";
        var dbSetName = NamingConventions.Pluralize(entity.Name);

        var body = command.Kind switch
        {
            CommandKind.Create => RenderCreateBody(entity, resultName, dbSetName),
            CommandKind.Update => RenderUpdateBody(entity, resultName, dbSetName),
            CommandKind.Delete => RenderDeleteBody(entity, resultName, dbSetName),
            _ => "throw new NotImplementedException();",
        };

        return new HandlerBinding(body, DependencyTypeName(context), "dbContext", HandlerUsings(context));
    }

    public HandlerBinding BindQueryHandler(GenerationContext context, FeatureModel feature, EntityModel entity, QueryModel query)
    {
        var resultName = $"{query.Name}Result";
        var dbSetName = NamingConventions.Pluralize(entity.Name);

        var body = query.IsCollection
            ? RenderGetAllBody(entity, resultName, dbSetName)
            : RenderGetByIdBody(entity, resultName, dbSetName);

        return new HandlerBinding(body, DependencyTypeName(context), "dbContext", HandlerUsings(context));
    }

    public IReadOnlyList<string> BuildProgramCsRegistration(GenerationContext context)
    {
        // Fully qualified rather than relying on a "using ...;" being present in Program.cs —
        // keeps this independent of whichever usings the caller composes.
        var qualifiedDbContextName = $"{context.Infrastructure.RootNamespace}.{DbContextName(context)}";
        var useCall = _kind == EfCoreProviderKind.Postgres
            ? "options.UseNpgsql(builder.Configuration.GetConnectionString(\"Default\"))"
            : "options.UseSqlServer(builder.Configuration.GetConnectionString(\"Default\"))";

        var lines = new List<string>
        {
            $"builder.Services.AddDbContext<{qualifiedDbContextName}>(options => {useCall});",
        };

        if (context.HasSeparateInfrastructure)
        {
            var qualifiedInterfaceName = $"{context.Application.RootNamespace}.Persistence.{DbContextInterfaceName(context)}";
            lines.Add($"builder.Services.AddScoped<{qualifiedInterfaceName}>(sp => sp.GetRequiredService<{qualifiedDbContextName}>());");
        }

        return lines;
    }

    private static string DbContextName(GenerationContext context) => $"{context.RootNamespace}DbContext";

    private static string DbContextInterfaceName(GenerationContext context) => $"I{context.RootNamespace}DbContext";

    private static string DependencyTypeName(GenerationContext context) =>
        context.HasSeparateInfrastructure ? DbContextInterfaceName(context) : DbContextName(context);

    private static List<string> HandlerUsings(GenerationContext context) => new()
    {
        context.HasSeparateInfrastructure ? $"{context.Application.RootNamespace}.Persistence" : context.Infrastructure.RootNamespace,
        $"{context.Domain.RootNamespace}.Entities",
        "Microsoft.EntityFrameworkCore",
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

    private static string RenderEntityConfiguration(GenerationContext context, EntityModel entity) =>
        $$"""
        using Microsoft.EntityFrameworkCore;
        using Microsoft.EntityFrameworkCore.Metadata.Builders;
        using {{context.Domain.RootNamespace}}.Entities;

        namespace {{context.Infrastructure.RootNamespace}}.Configurations;

        public sealed class {{entity.Name}}Configuration : IEntityTypeConfiguration<{{entity.Name}}>
        {
            public void Configure(EntityTypeBuilder<{{entity.Name}}> builder)
            {
                builder.HasKey(x => x.Id);
            }
        }
        """;

    private static string RenderDbContextInterface(GenerationContext context, IReadOnlyList<EntityReference> entities)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        if (entities.Count > 0)
        {
            // Only when there's at least one entity: a `using` for a namespace nothing has
            // declared yet (e.g. a fresh `new api` scaffold, before any entity exists) is a
            // compile error (CS0234), not a harmless no-op.
            sb.AppendLine($"using {context.Domain.RootNamespace}.Entities;");
        }

        sb.AppendLine();
        sb.AppendLine($"namespace {context.Application.RootNamespace}.Persistence;");
        sb.AppendLine();
        sb.AppendLine($"public interface {DbContextInterfaceName(context)}");
        sb.AppendLine("{");

        foreach (var reference in entities.DistinctBy(e => e.Entity.Name))
        {
            var dbSetName = NamingConventions.Pluralize(reference.Entity.Name);
            sb.AppendLine($"    DbSet<{reference.Entity.Name}> {dbSetName} {{ get; }}");
        }

        sb.AppendLine();
        sb.AppendLine("    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);");
        sb.Append('}');
        return sb.ToString();
    }

    private static string RenderDbContext(GenerationContext context, IReadOnlyList<EntityReference> entities)
    {
        var dbContextName = DbContextName(context);
        var sb = new StringBuilder();
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        if (entities.Count > 0)
        {
            sb.AppendLine($"using {context.Domain.RootNamespace}.Entities;");
        }

        if (context.HasSeparateInfrastructure)
        {
            sb.AppendLine($"using {context.Application.RootNamespace}.Persistence;");
        }

        sb.AppendLine();
        sb.AppendLine($"namespace {context.Infrastructure.RootNamespace};");
        sb.AppendLine();
        var baseList = context.HasSeparateInfrastructure ? $"DbContext, {DbContextInterfaceName(context)}" : "DbContext";
        sb.AppendLine($"public sealed class {dbContextName} : {baseList}");
        sb.AppendLine("{");
        sb.AppendLine($"    public {dbContextName}(DbContextOptions<{dbContextName}> options) : base(options)");
        sb.AppendLine("    {");
        sb.AppendLine("    }");
        sb.AppendLine();

        foreach (var reference in entities.DistinctBy(e => e.Entity.Name))
        {
            var dbSetName = NamingConventions.Pluralize(reference.Entity.Name);
            sb.AppendLine($"    public DbSet<{reference.Entity.Name}> {dbSetName} => Set<{reference.Entity.Name}>();");
        }

        sb.AppendLine();
        sb.AppendLine("    protected override void OnModelCreating(ModelBuilder modelBuilder)");
        sb.AppendLine("    {");
        sb.AppendLine($"        modelBuilder.ApplyConfigurationsFromAssembly(typeof({dbContextName}).Assembly);");
        sb.AppendLine("    }");
        sb.Append('}');
        return sb.ToString();
    }

    private static string RenderCreateBody(EntityModel entity, string resultName, string dbSetName)
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
        sb.AppendLine($"dbContext.{dbSetName}.Add(entity);");
        sb.AppendLine("await dbContext.SaveChangesAsync(cancellationToken);");
        sb.Append($"return new {resultName}(entity.Id);");
        return sb.ToString();
    }

    private static string RenderUpdateBody(EntityModel entity, string resultName, string dbSetName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"var entity = await dbContext.{dbSetName}.FindAsync(new object[] {{ message.Id }}, cancellationToken)");
        sb.AppendLine($"    ?? throw new KeyNotFoundException($\"{entity.Name} '{{message.Id}}' was not found.\");");
        foreach (var field in entity.Fields)
        {
            sb.AppendLine($"entity.{field.Name} = message.{field.Name};");
        }

        sb.AppendLine("await dbContext.SaveChangesAsync(cancellationToken);");
        sb.Append($"return new {resultName}(entity.Id);");
        return sb.ToString();
    }

    private static string RenderDeleteBody(EntityModel entity, string resultName, string dbSetName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"var entity = await dbContext.{dbSetName}.FindAsync(new object[] {{ message.Id }}, cancellationToken);");
        sb.AppendLine("if (entity is not null)");
        sb.AppendLine("{");
        sb.AppendLine($"    dbContext.{dbSetName}.Remove(entity);");
        sb.AppendLine("    await dbContext.SaveChangesAsync(cancellationToken);");
        sb.AppendLine("}");
        sb.Append($"return new {resultName}(message.Id);");
        return sb.ToString();
    }

    private static string RenderGetByIdBody(EntityModel entity, string resultName, string dbSetName)
    {
        var args = string.Join(", ", new[] { "entity.Id" }.Concat(entity.Fields.Select(f => $"entity.{f.Name}")));
        var sb = new StringBuilder();
        sb.AppendLine($"var entity = await dbContext.{dbSetName}.AsNoTracking().FirstOrDefaultAsync(x => x.Id == message.Id, cancellationToken)");
        sb.AppendLine($"    ?? throw new KeyNotFoundException($\"{entity.Name} '{{message.Id}}' was not found.\");");
        sb.Append($"return new {resultName}({args});");
        return sb.ToString();
    }

    private static string RenderGetAllBody(EntityModel entity, string resultName, string dbSetName)
    {
        var args = string.Join(", ", new[] { "entity.Id" }.Concat(entity.Fields.Select(f => $"entity.{f.Name}")));
        var sb = new StringBuilder();
        sb.AppendLine($"var entities = await dbContext.{dbSetName}.AsNoTracking().ToListAsync(cancellationToken);");
        sb.Append($"return entities.Select(entity => new {resultName}({args})).ToList();");
        return sb.ToString();
    }
}
