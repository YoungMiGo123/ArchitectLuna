using System.ComponentModel;
using ArchitectLuna.Cli.Adapters;
using ArchitectLuna.Cli.Scaffolding;
using ArchitectLuna.Core.Model;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ArchitectLuna.Cli.Commands;

public sealed class NewApiCommandSettings : CommandSettings
{
    [CommandArgument(0, "<name>")]
    public required string Name { get; init; }

    [CommandOption("--adapter")]
    [Description("Backend adapter: mediatr or wolverine.")]
    [DefaultValue("mediatr")]
    public string Adapter { get; init; } = "mediatr";

    [CommandOption("--persistence")]
    [Description("Persistence provider: in-memory (default), none, efcore-postgres, efcore-sqlserver, or marten.")]
    [DefaultValue("in-memory")]
    public string Persistence { get; init; } = "in-memory";

    [CommandOption("--architecture")]
    [Description("Solution layout: clean-architecture (default; Api/Application/Domain/Infrastructure projects) or vertical-slice (single Api project, features live inside it).")]
    [DefaultValue("clean-architecture")]
    public string Architecture { get; init; } = "clean-architecture";

    [CommandOption("--no-format")]
    [Description("Skip running 'dotnet format' over the scaffolded solution.")]
    [DefaultValue(false)]
    public bool NoFormat { get; init; }

    [CommandOption("--database-apply-mode")]
    [Description("When database changes are applied: manual (default), on-generate, or on-startup.")]
    [DefaultValue("manual")]
    public string DatabaseApplyMode { get; init; } = "manual";
}

public sealed class NewApiCommand : Command<NewApiCommandSettings>
{
    private static readonly string[] KnownArchitectures = { "vertical-slice", "clean-architecture" };

    protected override int Execute(CommandContext context, NewApiCommandSettings settings, CancellationToken cancellationToken)
    {
        if (!AdapterRegistry.KnownAdapters.Contains(settings.Adapter))
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Unknown adapter '{settings.Adapter}'. Valid values: {string.Join(", ", AdapterRegistry.KnownAdapters)}.[/]");
            return 1;
        }

        if (!PersistenceRegistry.KnownProviders.Contains(settings.Persistence))
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Unknown persistence provider '{settings.Persistence}'. Valid values: {string.Join(", ", PersistenceRegistry.KnownProviders)}.[/]");
            return 1;
        }

        if (!KnownArchitectures.Contains(settings.Architecture))
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Unknown architecture '{settings.Architecture}'. Valid values: {string.Join(", ", KnownArchitectures)}.[/]");
            return 1;
        }

        var layout = settings.Architecture == "clean-architecture" ? SolutionLayout.CleanArchitecture : SolutionLayout.VerticalSlice;

        Core.Model.DatabaseApplyMode applyMode;
        try
        {
            applyMode = DatabaseApplyModeParser.Parse(settings.DatabaseApplyMode);
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{ex.Message}[/]");
            return 1;
        }

        var root = SolutionScaffolder.Scaffold(Directory.GetCurrentDirectory(), settings.Name, settings.Adapter, settings.Persistence, layout, format: !settings.NoFormat, applyMode: applyMode);
        AnsiConsole.MarkupLineInterpolated($"[green]Created {settings.Name} at {root} (adapter: {settings.Adapter}, persistence: {settings.Persistence}, architecture: {settings.Architecture}, database apply mode: {settings.DatabaseApplyMode}).[/]");
        return 0;
    }
}
