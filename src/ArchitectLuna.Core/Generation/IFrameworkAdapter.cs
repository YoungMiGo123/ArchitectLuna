using ArchitectLuna.Core.Model;

namespace ArchitectLuna.Core.Generation;

/// <summary>
/// Produces the same set of file kinds (command/handler/validator/endpoint) from the Intent
/// Model regardless of backend, so switching --adapter changes implementation shape only.
/// </summary>
public interface IFrameworkAdapter
{
    /// <summary>
    /// Adapter key as used in model.yaml's "adapter" field and the --adapter CLI flag.
    /// </summary>
    string Name { get; }

    IReadOnlyList<GeneratedFile> GenerateCommand(GenerationContext context, FeatureModel feature, CommandModel command, ApiStyle apiStyle = ApiStyle.MinimalApi);

    IReadOnlyList<GeneratedFile> GenerateQuery(GenerationContext context, FeatureModel feature, QueryModel query, ApiStyle apiStyle = ApiStyle.MinimalApi);

    /// <summary>
    /// NuGet package IDs the generated API project must reference for this adapter's output to compile.
    /// </summary>
    IReadOnlyList<string> RequiredPackages { get; }
}
