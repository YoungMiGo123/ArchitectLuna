using ArchitectLuna.Core.Workspace;
using Spectre.Console;

namespace ArchitectLuna.Cli.Commands;

/// <summary>
/// Ordering Rule 1: nothing can be added outside an ArchitectLuna project. Every `add`/`generate`
/// command locates the workspace through this guard so running outside a project fails with the
/// same clear, actionable error instead of a stack trace.
/// </summary>
internal static class WorkspaceGuard
{
    public static bool TryLocateModelPath(out string modelPath)
    {
        try
        {
            var root = WorkspaceLocator.Locate(Directory.GetCurrentDirectory());
            modelPath = Path.Combine(root, ".architect", "model.yaml");
            return true;
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{ex.Message}[/]");
            modelPath = string.Empty;
            return false;
        }
    }
}
