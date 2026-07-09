using ArchitectLuna.Core.Model;
using ArchitectLuna.Core.Workspace;
using ArchitectLuna.Core.Yaml;

namespace ArchitectLuna.Ui.Infrastructure;

public sealed record WorkspaceLoadResult(bool Success, string? RootPath, string? ModelPath, ArchitectModel? Model, string? Error)
{
    public static WorkspaceLoadResult Failure(string error) => new(false, null, null, null, error);
}

/// <summary>
/// Resolves a user-supplied solution root path to its <c>.architect/model.yaml</c> and loads it
/// via <see cref="ModelSerializer"/> directly — the same Core APIs the CLI uses, since Core has
/// zero console I/O and is meant to be consumed by a UI directly (see docs/ROADMAP.md).
/// </summary>
public static class WorkspaceLoader
{
    public static WorkspaceLoadResult Load(string? rootPathInput)
    {
        if (string.IsNullOrWhiteSpace(rootPathInput))
        {
            return WorkspaceLoadResult.Failure("Enter a solution root directory path to get started.");
        }

        string root;
        try
        {
            if (!Directory.Exists(rootPathInput))
            {
                return WorkspaceLoadResult.Failure($"Directory '{rootPathInput}' does not exist.");
            }

            root = WorkspaceLocator.Locate(Path.GetFullPath(rootPathInput));
        }
        catch (InvalidOperationException ex)
        {
            return WorkspaceLoadResult.Failure(ex.Message);
        }

        var modelPath = Path.Combine(root, ".architect", "model.yaml");
        try
        {
            var model = ModelSerializer.Load(modelPath);
            return new WorkspaceLoadResult(true, root, modelPath, model, null);
        }
        catch (Exception ex)
        {
            return WorkspaceLoadResult.Failure($"Failed to load '{modelPath}': {ex.Message}");
        }
    }
}
