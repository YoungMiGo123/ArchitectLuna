using ArchitectLuna.Core.Editing;
using ArchitectLuna.Core.Model;
using Spectre.Console;

namespace ArchitectLuna.Cli.Commands;

/// <summary>
/// docs/requirements/003-improvements.md §8, §8.1: `add entity`/`add crud` offer to create a
/// missing feature instead of just failing outright — interactively via a Y/n prompt, or
/// automatically with `--yes`/`--create-missing`. Shared by both commands so the prompt wording,
/// non-interactive detection, and cancellation behavior stay identical between them.
/// </summary>
internal static class CompoundCommandSupport
{
    /// <summary>
    /// Ensures <paramref name="featureName"/> exists on <paramref name="model"/>, creating it if
    /// missing and permitted. Returns false (with a message already printed) when the feature is
    /// missing and creation wasn't permitted or was declined — callers must not proceed.
    /// </summary>
    public static bool TryEnsureFeatureExists(ArchitectModel model, string featureName, bool allowCreateMissing)
    {
        if (model.Features.Any(f => f.Name == featureName))
        {
            return true;
        }

        if (allowCreateMissing)
        {
            ModelEditor.AddFeature(model, featureName);
            AnsiConsole.MarkupLineInterpolated($"[green]Created feature '{featureName}'.[/]");
            return true;
        }

        if (!IsInteractive())
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Feature '{featureName}' does not exist. Run 'architect-luna add feature {featureName}' first, or re-run with --yes (or --create-missing) to create it automatically in non-interactive mode.[/]");
            return false;
        }

        AnsiConsole.MarkupLineInterpolated($"[yellow]Feature '{featureName}' does not exist.[/]");
        if (!AnsiConsole.Confirm("Create it now?"))
        {
            AnsiConsole.MarkupLine("[red]Operation cancelled.[/]");
            return false;
        }

        ModelEditor.AddFeature(model, featureName);
        AnsiConsole.MarkupLineInterpolated($"[green]Created feature '{featureName}'.[/]");
        return true;
    }

    /// <summary>
    /// A prompt that can't be answered (piped stdin/stdout, e.g. CI) must never hang — treat it
    /// the same as "no permission to create missing dependencies" and fail clearly instead.
    /// </summary>
    private static bool IsInteractive() => !Console.IsInputRedirected && !Console.IsOutputRedirected;
}
