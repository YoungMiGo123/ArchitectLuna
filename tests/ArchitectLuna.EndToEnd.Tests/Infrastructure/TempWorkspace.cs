namespace ArchitectLuna.EndToEnd.Tests.Infrastructure;

/// <summary>
/// Creates and cleans up scratch directories for end-to-end runs. Always rooted under the OS temp
/// directory, never inside the repo, so scaffolded solutions/builds never pollute the working tree.
/// </summary>
public static class TempWorkspace
{
    public static string CreateUnique()
    {
        var path = Path.Combine(Path.GetTempPath(), "architect-luna-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// Best-effort recursive delete. Generated solutions include bin/obj output that can briefly
    /// hold file locks (e.g. from an MSBuild node not yet torn down) — swallow failures rather
    /// than letting cleanup itself fail the test after the real assertions already ran.
    /// </summary>
    public static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // best effort cleanup — do not fail the test over leftover temp files.
        }
    }
}
