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
    [Description("Solution layout: vertical-slice (single Api project, features live inside it) or clean-architecture (Api/Application/Domain/Infrastructure projects).")]
    [DefaultValue("vertical-slice")]
    public string Architecture { get; init; } = "vertical-slice";
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

        var root = SolutionScaffolder.Scaffold(Directory.GetCurrentDirectory(), settings.Name, settings.Adapter, settings.Persistence, layout);
        AnsiConsole.MarkupLineInterpolated($"[green]Created {settings.Name} at {root} (adapter: {settings.Adapter}, persistence: {settings.Persistence}, architecture: {settings.Architecture}).[/]");
        return 0;
    }
}
