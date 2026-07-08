namespace ArchitectLuna.Core.Generation;

/// <summary>
/// Everything an adapter needs to place and namespace generated files, independent of the
/// on-disk layout decisions made by the CLI's scaffolder.
/// </summary>
public sealed record GenerationContext(string RootNamespace, string ProjectRelativeRoot);
