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
/// Generates, per entity, a domain class (inheriting the scaffolded <c>BaseEntity</c>) under
/// <see cref="GenerationContext.Domain"/>/Entities and an IEntityTypeConfiguration under
/// <see cref="GenerationContext.Infrastructure"/>/Configurations; once per solution, a DbContext
/// in Infrastructure with one DbSet per entity. For vertical slice, Domain and Infrastructure are
/// the same project (a "Persistence" subfolder of the API project), so this produces exactly the
/// same files/namespaces as before this context became multi-root. For Clean Architecture, Domain
/// and Infrastructure are genuinely separate projects — Application must not reference
/// Infrastructure directly, so an <c>I{Solution}DbContext</c> interface is generated in
/// Application (see <see cref="GenerationContext.HasSeparateInfrastructure"/>) and the concrete
/// DbContext in Infrastructure implements it; handlers depend on the interface instead of the
/// concrete type.
///
/// Handler bodies get the DbContext (or its interface) constructor/method-injected (MediatR via
/// constructor, Wolverine via an extra static-method parameter), do straightforward
/// Add/Find/Remove/SaveChanges calls — no repository layer, matching the tool's "simplistic"
/// generated-code philosophy — and return <c>Result&lt;T&gt;</c> outcomes (NotFound as an
/// explicit failure, never an exception for a normal business outcome).
/// </summary>
public sealed class EfCorePersistenceGenerator : IPersistenceGenerator
{
    private readonly EfCoreProviderKind _kind;

    public EfCorePersistenceGenerator(EfCoreProviderKind kind)
    {
        _kind = kind;
    }

    public string Name => _kind == EfCoreProviderKind.Postgres ? "efcore-postgres" : "efcore-sqlserver";

    // Microsoft.EntityFrameworkCore.Design goes on the same project as the concrete DbContext
    // (Infrastructure, or the Api project itself for vertical slice) — that's the project
    // `dotnet ef migrations add --project ...` targets, and EF's tooling refuses to run without
    // this package referenced there (docs/requirements/003-improvements.md §10.1).
    public IReadOnlyList<string> RequiredPackages => _kind == EfCoreProviderKind.Postgres
        ? new[] { "Microsoft.EntityFrameworkCore", "Npgsql.EntityFrameworkCore.PostgreSQL", "Microsoft.EntityFrameworkCore.Design" }
        : new[] { "Microsoft.EntityFrameworkCore", "Microsoft.EntityFrameworkCore.SqlServer", "Microsoft.EntityFrameworkCore.Design" };

    // Handler bodies (dbContext.Add/Find/Remove/SaveChangesAsync) and, under Clean Architecture,
    // the I{Solution}DbContext interface's DbSet<T> properties both live in Application — neither
    // needs the database-specific provider package, only EF Core's own abstractions.
    public IReadOnlyList<string> ApplicationRequiredPackages { get; } = new[] { "Microsoft.EntityFrameworkCore" };

    public IReadOnlyList<string> ServiceRegistrationUsings { get; } = new[] { "Microsoft.EntityFrameworkCore" };

    public IReadOnlyList<string> BuildStartupApplyLines(GenerationContext context)
    {
        // Always the concrete DbContext, never the I{Solution}DbContext abstraction used
        // elsewhere under Clean Architecture — `.Database.Migrate()` is an EF Core DbContext
        // member, not part of the hand-rolled interface (see DependencyTypeName's doc comment).
        var dbContextName = DbContextName(context);
        return new[]
        {
            "using (var scope = app.Services.CreateScope())",
            "{",
            $"    scope.ServiceProvider.GetRequiredService<{dbContextName}>().Database.Migrate();",
            "}",
        };
    }

    // "Microsoft.EntityFrameworkCore" is required for the DbContext.Database.Migrate()
    // extension method to resolve — Program.cs otherwise has no reason to reference EF Core.
    public IReadOnlyList<string> StartupApplyUsings { get; } = new[] { "Microsoft.Extensions.DependencyInjection", "Microsoft.EntityFrameworkCore" };

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
        // exists: the generated AddInfrastructure references this type unconditionally as soon as
        // EF Core persistence is configured, so the freshly scaffolded solution (before the first
        // `generate`) needs it to already exist and compile.
        var files = new List<GeneratedFile>
        {
            new($"{context.Infrastructure.ProjectRoot}/{DbContextName(context)}.cs", RenderDbContext(context, entities)),
            new($"{context.Infrastructure.ProjectRoot}/Persistence/{DbContextName(context)}Factory.cs", RenderDbContextFactory(context)),
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

        var body = query.IsPaged
            ? RenderPagedGetAllBody(entity, resultName, dbSetName)
            : query.IsCollection
                ? RenderGetAllBody(entity, resultName, dbSetName)
                : RenderGetByIdBody(entity, resultName, dbSetName);

        return new HandlerBinding(body, DependencyTypeName(context), "dbContext", HandlerUsings(context));
    }

    public IReadOnlyList<string> BuildServiceRegistration(GenerationContext context)
    {
        // Fully qualified rather than relying on a "using ...;" being present in the generated
        // AddInfrastructure file — keeps this independent of whichever usings the caller composes.
        var qualifiedDbContextName = $"{context.Infrastructure.RootNamespace}.{DbContextName(context)}";
        var useCall = _kind == EfCoreProviderKind.Postgres
            ? "options.UseNpgsql(configuration.GetConnectionString(\"Default\"))"
            : "options.UseSqlServer(configuration.GetConnectionString(\"Default\"))";

        var lines = new List<string>
        {
            $"services.AddDbContext<{qualifiedDbContextName}>(options => {useCall});",
        };

        if (context.HasSeparateInfrastructure)
        {
            var qualifiedInterfaceName = $"{context.Application.RootNamespace}.Persistence.{DbContextInterfaceName(context)}";
            lines.Add($"services.AddScoped<{qualifiedInterfaceName}>(sp => sp.GetRequiredService<{qualifiedDbContextName}>());");
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
        $"{context.Application.RootNamespace}.Common.Results",
        $"{context.Domain.RootNamespace}.Entities",
        "Microsoft.EntityFrameworkCore",
    };

    private static string RenderEntityClass(GenerationContext context, EntityModel entity)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"using {context.Domain.RootNamespace}.Common;");
        sb.AppendLine();
        sb.AppendLine($"namespace {context.Domain.RootNamespace}.Entities;");
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

    /// <summary>
    /// Lets EF Core tooling create a <c>DbContext</c> without going through the app's runtime DI
    /// container (docs/requirements/003-improvements.md §10.2) — without this, `dotnet ef
    /// migrations add`/`database update` fail trying to resolve `DbContextOptions&lt;T&gt;` from a
    /// container that only exists once `WebApplication.CreateBuilder` runs. Uses the same local
    /// development connection string shape as the scaffolded appsettings.Development.json; pass
    /// `--connection` to `dotnet ef` to target a different database.
    /// </summary>
    private string RenderDbContextFactory(GenerationContext context)
    {
        var dbContextName = DbContextName(context);
        var localConnectionString = _kind == EfCoreProviderKind.Postgres
            ? $"Host=localhost;Database={context.RootNamespace.ToLowerInvariant()};Username=postgres;Password=postgres"
            : $"Server=localhost;Database={context.RootNamespace};Trusted_Connection=True;TrustServerCertificate=True";
        var useCall = _kind == EfCoreProviderKind.Postgres
            ? $"optionsBuilder.UseNpgsql(\"{localConnectionString}\")"
            : $"optionsBuilder.UseSqlServer(\"{localConnectionString}\")";

        return $$"""
        using Microsoft.EntityFrameworkCore;
        using Microsoft.EntityFrameworkCore.Design;
        using {{context.Infrastructure.RootNamespace}};

        namespace {{context.Infrastructure.RootNamespace}}.Persistence;

        /// <summary>
        /// Design-time factory so `dotnet ef migrations add`/`database update` can create a
        /// {{dbContextName}} without the app's runtime DI container. Uses the local development
        /// connection string; pass --connection to target a different database.
        /// </summary>
        public sealed class {{dbContextName}}Factory : IDesignTimeDbContextFactory<{{dbContextName}}>
        {
            public {{dbContextName}} CreateDbContext(string[] args)
            {
                var optionsBuilder = new DbContextOptionsBuilder<{{dbContextName}}>();
                {{useCall}};
                return new {{dbContextName}}(optionsBuilder.Options);
            }
        }
        """;
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
        sb.Append($"return Result<{resultName}>.Success(new {resultName}(entity.Id));");
        return sb.ToString();
    }

    private static string RenderUpdateBody(EntityModel entity, string resultName, string dbSetName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"var entity = await dbContext.{dbSetName}.FindAsync(new object[] {{ message.Id }}, cancellationToken);");
        sb.AppendLine("if (entity is null)");
        sb.AppendLine("{");
        sb.AppendLine($"    return Result<{resultName}>.Failure(Error.NotFound($\"{entity.Name} '{{message.Id}}' was not found.\"));");
        sb.AppendLine("}");
        sb.AppendLine();
        foreach (var field in entity.Fields)
        {
            sb.AppendLine($"entity.{field.Name} = message.{field.Name};");
        }

        sb.AppendLine("await dbContext.SaveChangesAsync(cancellationToken);");
        sb.Append($"return Result<{resultName}>.Success(new {resultName}(entity.Id));");
        return sb.ToString();
    }

    private static string RenderDeleteBody(EntityModel entity, string resultName, string dbSetName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"var entity = await dbContext.{dbSetName}.FindAsync(new object[] {{ message.Id }}, cancellationToken);");
        sb.AppendLine("if (entity is null)");
        sb.AppendLine("{");
        sb.AppendLine($"    return Result<{resultName}>.Failure(Error.NotFound($\"{entity.Name} '{{message.Id}}' was not found.\"));");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine($"dbContext.{dbSetName}.Remove(entity);");
        sb.AppendLine("await dbContext.SaveChangesAsync(cancellationToken);");
        sb.Append($"return Result<{resultName}>.Success(new {resultName}(message.Id));");
        return sb.ToString();
    }

    private static string RenderGetByIdBody(EntityModel entity, string resultName, string dbSetName)
    {
        var args = string.Join(", ", new[] { "entity.Id" }.Concat(entity.Fields.Select(f => $"entity.{f.Name}")));
        var sb = new StringBuilder();
        sb.AppendLine($"var entity = await dbContext.{dbSetName}.AsNoTracking().FirstOrDefaultAsync(x => x.Id == message.Id, cancellationToken);");
        sb.AppendLine("if (entity is null)");
        sb.AppendLine("{");
        sb.AppendLine($"    return Result<{resultName}>.Failure(Error.NotFound($\"{entity.Name} '{{message.Id}}' was not found.\"));");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.Append($"return Result<{resultName}>.Success(new {resultName}({args}));");
        return sb.ToString();
    }

    private static string RenderGetAllBody(EntityModel entity, string resultName, string dbSetName)
    {
        var args = string.Join(", ", new[] { "entity.Id" }.Concat(entity.Fields.Select(f => $"entity.{f.Name}")));
        var sb = new StringBuilder();
        sb.AppendLine($"var entities = await dbContext.{dbSetName}.AsNoTracking().ToListAsync(cancellationToken);");
        sb.Append($"return Result<IReadOnlyList<{resultName}>>.Success(entities.Select(entity => new {resultName}({args})).ToList());");
        return sb.ToString();
    }

    private static string RenderPagedGetAllBody(EntityModel entity, string resultName, string dbSetName)
    {
        var args = string.Join(", ", new[] { "entity.Id" }.Concat(entity.Fields.Select(f => $"entity.{f.Name}")));
        var sb = new StringBuilder();
        sb.AppendLine("var page = message.Page <= 0 ? 1 : message.Page;");
        sb.AppendLine("var pageSize = message.PageSize <= 0 ? 20 : message.PageSize;");
        sb.AppendLine($"var totalCount = await dbContext.{dbSetName}.LongCountAsync(cancellationToken);");
        sb.AppendLine($"var entities = await dbContext.{dbSetName}.AsNoTracking().OrderBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);");
        sb.Append($"return Result<PagedResult<{resultName}>>.Success(new PagedResult<{resultName}>(entities.Select(entity => new {resultName}({args})).ToList(), page, pageSize, totalCount));");
        return sb.ToString();
    }
}
