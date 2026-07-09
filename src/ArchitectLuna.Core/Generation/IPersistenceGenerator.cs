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
    /// <summary>Provider key as used in the --persistence CLI flag ("none", "efcore-postgres", "efcore-sqlserver", "marten").</summary>
    string Name { get; }

    /// <summary>NuGet package IDs the generated API project must reference for this provider to compile.</summary>
    IReadOnlyList<string> RequiredPackages { get; }

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
    /// Return an empty list for "no registration needed."
    /// </summary>
    IReadOnlyList<string> BuildProgramCsRegistration(string solutionName);

    /// <summary>Using directives BuildProgramCsRegistration's lines need, e.g. "Microsoft.EntityFrameworkCore".</summary>
    IReadOnlyList<string> ProgramCsUsings { get; }
}
