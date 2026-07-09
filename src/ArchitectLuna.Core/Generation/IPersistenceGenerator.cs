using ArchitectLuna.Core.Model;

namespace ArchitectLuna.Core.Generation;

/// <summary>
/// Plugs real persistence into entity-backed CRUD handlers. Orthogonal to
/// <see cref="IFrameworkAdapter"/>: a messaging adapter renders the message/handler/validator/
/// endpoint file shapes and asks its configured <see cref="IPersistenceGenerator"/> for the
/// handler binding (body + injected dependency) and any generated persistence files — it never
/// needs to know which concrete provider is in play.
/// </summary>
public interface IPersistenceGenerator
{
    /// <summary>Provider key as used in the --persistence CLI flag ("none", "in-memory", "efcore-postgres", "efcore-sqlserver", "marten").</summary>
    string Name { get; }

    /// <summary>NuGet package IDs the project owning the concrete implementation (Infrastructure, or the Api project itself for vertical slice) must reference for this provider to compile.</summary>
    IReadOnlyList<string> RequiredPackages { get; }

    /// <summary>
    /// NuGet package IDs the Application project must reference. Handler bodies (Store/Load/
    /// SaveChanges-style calls) always live in Application, so it needs at least the persistence
    /// library's own types even when it doesn't own the concrete implementation — e.g. EF Core's
    /// bare `Microsoft.EntityFrameworkCore` for the `DbSet&lt;T&gt;`-shaped interface it owns under
    /// Clean Architecture (never the database-specific provider package, which is an Infrastructure
    /// concern only). For vertical slice, Application and Infrastructure are the same project, so
    /// this is a strict subset of <see cref="RequiredPackages"/> already covered there — the caller
    /// only needs to add it separately when <see cref="GenerationContext.HasSeparateInfrastructure"/>.
    /// </summary>
    IReadOnlyList<string> ApplicationRequiredPackages { get; }

    /// <summary>
    /// Files generated once per entity (e.g. an EF Core `IEntityTypeConfiguration&lt;T&gt;`
    /// class). Called once per entity during `generate`.
    /// </summary>
    IReadOnlyList<GeneratedFile> GenerateEntityPersistence(GenerationContext context, FeatureModel feature, EntityModel entity);

    /// <summary>
    /// Solution-level file(s) needing visibility across every entity at once — a DbContext with
    /// one DbSet per entity, for example. Called once per `generate` run with every known entity.
    /// Returns an empty list for providers with nothing solution-level to emit (e.g. Marten,
    /// which just needs per-call session access, no aggregate registry file).
    /// </summary>
    IReadOnlyList<GeneratedFile> GenerateSolutionPersistence(GenerationContext context, IReadOnlyList<EntityReference> entities);

    /// <summary>
    /// The handler binding for a command targeting the given entity. Only ever called for a
    /// command with a non-null <see cref="CommandModel.EntityName"/>.
    /// </summary>
    HandlerBinding BindCommandHandler(GenerationContext context, FeatureModel feature, EntityModel entity, CommandModel command);

    /// <summary>Same as <see cref="BindCommandHandler"/> but for a query handler.</summary>
    HandlerBinding BindQueryHandler(GenerationContext context, FeatureModel feature, EntityModel entity, QueryModel query);

    /// <summary>
    /// Source lines to splice into the scaffolded Program.cs (after the adapter's own bootstrap,
    /// before the app is built) to register this provider, e.g. `builder.Services.AddDbContext...`.
    /// Takes the full <see cref="GenerationContext"/> (not just the solution name) so registration
    /// can correctly qualify the concrete type's namespace under <see cref="GenerationContext.Infrastructure"/>
    /// and, when <see cref="GenerationContext.HasSeparateInfrastructure"/>, also wire the interface
    /// abstraction living in <see cref="GenerationContext.Application"/>. Return an empty list for
    /// "no registration needed."
    /// </summary>
    IReadOnlyList<string> BuildProgramCsRegistration(GenerationContext context);

    /// <summary>Using directives BuildProgramCsRegistration's lines need, e.g. "Microsoft.EntityFrameworkCore".</summary>
    IReadOnlyList<string> ProgramCsUsings { get; }
}
