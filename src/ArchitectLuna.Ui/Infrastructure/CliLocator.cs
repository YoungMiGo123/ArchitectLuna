namespace ArchitectLuna.Ui.Infrastructure;

/// <summary>
/// Finds the built architect-luna CLI executable so the "Generate" page can shell out to it,
/// the same way <c>SolutionScaffolder</c> shells out to <c>dotnet</c>. We locate it relative to
/// a well-known marker (the repo's <c>ArchitectLuna.sln</c>) instead of hardcoding an absolute
/// path, by walking up from this app's own base directory the same way
/// <c>ArchitectLuna.Core.Workspace.WorkspaceLocator</c> walks up looking for
/// <c>.architect/model.yaml</c>.
/// </summary>
public static class CliLocator
{
    private const string SolutionMarker = "ArchitectLuna.sln";

    /// <summary>
    /// Locates the built <c>architect-luna(.dll)</c>. Returns null if the repo root marker or a
    /// built CLI output couldn't be found (e.g. the CLI hasn't been built yet).
    /// </summary>
    public static string? FindCliDll()
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        if (repoRoot is null)
        {
            return null;
        }

        var cliProjectDir = Path.Combine(repoRoot, "src", "ArchitectLuna.Cli");
        if (!Directory.Exists(cliProjectDir))
        {
            return null;
        }

        foreach (var configuration in new[] { "Debug", "Release" })
        {
            var candidate = Path.Combine(cliProjectDir, "bin", configuration, "net10.0", "architect-luna.dll");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? FindRepoRoot(string startDirectory)
    {
        var dir = new DirectoryInfo(startDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, SolutionMarker)))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
