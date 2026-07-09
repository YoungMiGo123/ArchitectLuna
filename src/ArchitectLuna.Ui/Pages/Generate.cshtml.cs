using ArchitectLuna.Core.Model;
using ArchitectLuna.Ui.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ArchitectLuna.Ui.Pages;

/// <summary>
/// Shells out to the already-built architect-luna CLI's "generate" command, the same
/// Process.Start technique SolutionScaffolder.cs uses for dotnet calls, rather than
/// re-implementing generation in the UI.
/// </summary>
public sealed class GenerateModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Root { get; set; }

    public ArchitectModel? LoadedModel { get; private set; }

    public string? ResolvedRoot { get; private set; }

    public string? ErrorMessage { get; private set; }

    public bool HasRun { get; private set; }

    public bool RunSucceeded { get; private set; }

    public string? Output { get; private set; }

    public void OnGet()
    {
        LoadWorkspace();
    }

    public void OnPost()
    {
        if (LoadWorkspace() is null)
        {
            return;
        }

        var cliDll = CliLocator.FindCliDll();
        if (cliDll is null)
        {
            ErrorMessage = "Could not locate the built architect-luna CLI. Build it first with " +
                            "'dotnet build src/ArchitectLuna.Cli/ArchitectLuna.Cli.csproj'.";
            return;
        }

        var result = CliRunner.Run(cliDll, ResolvedRoot!, "generate");
        HasRun = true;
        RunSucceeded = result.Started && result.ExitCode == 0;
        Output = (result.StandardOutput + result.StandardError).Trim();

        if (!result.Started)
        {
            ErrorMessage = result.StandardError;
        }

        // Reload the model in case generate touched it (it doesn't today, but manifest state
        // does change, and staying consistent with the file on disk is the safe default).
        LoadWorkspace();
    }

    private ArchitectModel? LoadWorkspace()
    {
        var result = WorkspaceLoader.Load(Root);
        if (!result.Success)
        {
            ErrorMessage = result.Error;
            return null;
        }

        LoadedModel = result.Model;
        ResolvedRoot = result.RootPath;
        return result.Model;
    }
}
