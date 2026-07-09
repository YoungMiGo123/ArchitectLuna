using ArchitectLuna.Core.Model;
using ArchitectLuna.Ui.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ArchitectLuna.Ui.Pages;

public sealed class IndexModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Root { get; set; }

    public ArchitectModel? LoadedModel { get; private set; }

    public string? ResolvedRoot { get; private set; }

    public string? ErrorMessage { get; private set; }

    public void OnGet()
    {
        if (string.IsNullOrWhiteSpace(Root))
        {
            // Nothing entered yet - show the picker form only, no error.
            return;
        }

        var result = WorkspaceLoader.Load(Root);
        if (!result.Success)
        {
            ErrorMessage = result.Error;
            return;
        }

        LoadedModel = result.Model;
        ResolvedRoot = result.RootPath;
    }
}
