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

    public IReadOnlyList<string> ProgramCsUsings { get; } = Array.Empty<string>();

    public IReadOnlyList<GeneratedFile> GenerateEntityPersistence(GenerationContext context, FeatureModel feature, EntityModel entity) =>
        Array.Empty<GeneratedFile>();

    public IReadOnlyList<GeneratedFile> GenerateSolutionPersistence(GenerationContext context, IReadOnlyList<EntityReference> entities) =>
        Array.Empty<GeneratedFile>();

    public HandlerBinding BindCommandHandler(GenerationContext context, FeatureModel feature, EntityModel entity, CommandModel command) =>
        HandlerBinding.NotImplemented();

    public HandlerBinding BindQueryHandler(GenerationContext context, FeatureModel feature, EntityModel entity, QueryModel query) =>
        HandlerBinding.NotImplemented();

    public IReadOnlyList<string> BuildProgramCsRegistration(string solutionName) => Array.Empty<string>();
}
