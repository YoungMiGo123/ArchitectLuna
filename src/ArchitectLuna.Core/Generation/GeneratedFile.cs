namespace ArchitectLuna.Core.Generation;

/// <summary>
/// One rendered output file. <see cref="RelativePath"/> is relative to the solution root.
/// </summary>
public sealed record GeneratedFile(string RelativePath, string Content);
