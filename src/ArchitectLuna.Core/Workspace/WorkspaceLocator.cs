namespace ArchitectLuna.Core.Workspace;

/// <summary>
/// Finds the solution root (the directory containing .architect/model.yaml) by walking up from
/// a starting directory, the same way git locates a repository root.
/// </summary>
public static class WorkspaceLocator
{
    public static string Locate(string startDirectory)
    {
        var dir = new DirectoryInfo(startDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, ".architect", "model.yaml")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Could not find .architect/model.yaml in this directory or any parent. " +
            "Run this command from inside a solution created with 'architect-luna new api'.");
    }
}
