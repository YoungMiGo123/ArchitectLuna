using ArchitectLuna.Core.Model;

namespace ArchitectLuna.Core.Generation;

/// <summary>
/// The default "no persistence configured" provider: handler bodies stay the original
/// NotImplementedException placeholder, and nothing else is generated or registered.
/// </summary>
public sealed class NullPersistenceGenerator : IPersistenceGenerator
{
    public string Name => "none";

    public IReadOnlyList<string> RequiredPackages { get; } = Array.Empty<string>();

    public IReadOnlyList<string> ApplicationRequiredPackages { get; } = Array.Empty<string>();

    public IReadOnlyList<GeneratedFile> GenerateEntityPersistence(GenerationContext context, FeatureModel feature, EntityModel entity) =>
        Array.Empty<GeneratedFile>();

    public IReadOnlyList<GeneratedFile> GenerateSolutionPersistence(GenerationContext context, IReadOnlyList<EntityReference> entities, DatabaseApplyMode applyMode) =>
        new[] { new GeneratedFile($"{context.Infrastructure.ProjectRoot}/PersistenceRegistration.cs", RenderNoOpAddPersistence(context)) };

    private static string RenderNoOpAddPersistence(GenerationContext context) =>
        $$"""
        using Microsoft.Extensions.Configuration;
        using Microsoft.Extensions.DependencyInjection;

        namespace {{context.Infrastructure.RootNamespace}};

        /// <summary>
        /// No persistence configured (--persistence none): handlers hold protected placeholders,
        /// so there is nothing to register. Present (as a no-op) so AddInfrastructure can call
        /// AddPersistence unconditionally regardless of the chosen provider.
        /// </summary>
        public static class PersistenceRegistration
        {
            public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration) => services;
        }
        """;

    public HandlerBinding BindCommandHandler(GenerationContext context, FeatureModel feature, EntityModel entity, CommandModel command) =>
        HandlerBinding.NotImplemented();

    public HandlerBinding BindQueryHandler(GenerationContext context, FeatureModel feature, EntityModel entity, QueryModel query) =>
        HandlerBinding.NotImplemented();
}
