using ArchitectLuna.Core.Model;
using ArchitectLuna.Core.Yaml;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ArchitectLuna.Cli.Commands;

public sealed class ConfigSetCommandSettings : CommandSettings
{
    [CommandArgument(0, "<key>")]
    public required string Key { get; init; }

    [CommandArgument(1, "<value>")]
    public required string Value { get; init; }
}

/// <summary>
/// docs/requirements/003-improvements.md §9.2: `database.applyMode` is the only supported key
/// today — a top-level `database:` block on <see cref="ArchitectModel"/>, not a separate config
/// file, since model.yaml is already the single source of truth (docs/ARCHITECTURE.md).
/// </summary>
public sealed class ConfigSetCommand : Command<ConfigSetCommandSettings>
{
    private const string DatabaseApplyModeKey = "database.applyMode";

    protected override int Execute(CommandContext context, ConfigSetCommandSettings settings, CancellationToken cancellationToken)
    {
        if (!WorkspaceGuard.TryLocateModelPath(out var modelPath))
        {
            return 1;
        }

        if (!string.Equals(settings.Key, DatabaseApplyModeKey, StringComparison.Ordinal))
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Unknown config key '{settings.Key}'. Supported keys: {DatabaseApplyModeKey}.[/]");
            return 1;
        }

        DatabaseApplyMode applyMode;
        try
        {
            applyMode = DatabaseApplyModeParser.Parse(settings.Value);
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{ex.Message}[/]");
            return 1;
        }

        var model = ModelSerializer.Load(modelPath);
        model.Database.ApplyMode = applyMode;
        ModelSerializer.Save(modelPath, model);

        AnsiConsole.MarkupLineInterpolated($"[green]Set {DatabaseApplyModeKey} = {settings.Value}.[/]");
        return 0;
    }
}
