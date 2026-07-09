namespace ArchitectLuna.EndToEnd.Tests.Infrastructure;

/// <summary>
/// Locates the built architect-luna CLI dll without any hardcoded absolute path, so these tests
/// work in any checkout location / CI runner.
///
/// Uses the same "walk up from a starting directory until a marker is found" technique as
/// ArchitectLuna.Core.Workspace.WorkspaceLocator (which walks up looking for
/// .architect/model.yaml) — here the marker is ArchitectLuna.sln, found relative to the *test
/// assembly's own location* (AppContext.BaseDirectory), not the process's current directory.
/// From the repo root we then descend into
/// src/ArchitectLuna.Cli/bin/{Debug|Release}/net10.0/architect-luna.dll.
///
/// An ARCHITECTLUNA_CLI_PATH environment variable, if set, always wins — an escape hatch for CI
/// setups that build the CLI to a nonstandard output path.
/// </summary>
public static class CliLocator
{
    private const string EnvVarOverride = "ARCHITECTLUNA_CLI_PATH";
    private const string SolutionFileName = "ArchitectLuna.sln";
    private const string CliRelativeProjectDir = "src/ArchitectLuna.Cli";
    private const string CliDllName = "architect-luna.dll";
    private const string TargetFramework = "net10.0";

    /// <summary>Resolves the full path to the built architect-luna.dll, or throws with a clear message.</summary>
    public static string ResolveCliDllPath()
    {
        var overridePath = Environment.GetEnvironmentVariable(EnvVarOverride);
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            if (!File.Exists(overridePath))
            {
                throw new InvalidOperationException(
                    $"{EnvVarOverride} was set to '{overridePath}' but no file exists there.");
            }

            return overridePath;
        }

        var repoRoot = LocateRepoRoot(AppContext.BaseDirectory);
        var cliProjectDir = Path.Combine(repoRoot, CliRelativeProjectDir.Replace('/', Path.DirectorySeparatorChar));

        foreach (var configuration in new[] { "Debug", "Release" })
        {
            var candidate = Path.Combine(cliProjectDir, "bin", configuration, TargetFramework, CliDllName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            $"Could not find a built {CliDllName} under '{cliProjectDir}/bin/{{Debug|Release}}/{TargetFramework}/'. " +
            $"Build it first with 'dotnet build {CliRelativeProjectDir}/ArchitectLuna.Cli.csproj', or set the " +
            $"{EnvVarOverride} environment variable to an explicit path.");
    }

    /// <summary>Walks up from <paramref name="startDirectory"/> until a directory containing ArchitectLuna.sln is found.</summary>
    public static string LocateRepoRoot(string startDirectory)
    {
        var dir = new DirectoryInfo(startDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, SolutionFileName)))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not find {SolutionFileName} in '{startDirectory}' or any parent directory. " +
            "These end-to-end tests must run from within an ArchitectLuna checkout.");
    }
}
