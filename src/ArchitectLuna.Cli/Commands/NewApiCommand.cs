using System.ComponentModel;
using ArchitectLuna.Cli.Adapters;
using ArchitectLuna.Cli.Scaffolding;
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
    [Description("Persistence provider: none, efcore-postgres, efcore-sqlserver, or marten.")]
    [DefaultValue("none")]
    public string Persistence { get; init; } = "none";
}

public sealed class NewApiCommand : Command<NewApiCommandSettings>
{
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

        var root = SolutionScaffolder.Scaffold(Directory.GetCurrentDirectory(), settings.Name, settings.Adapter, settings.Persistence);
        AnsiConsole.MarkupLineInterpolated($"[green]Created {settings.Name} at {root} (adapter: {settings.Adapter}, persistence: {settings.Persistence}).[/]");
        return 0;
    }
}
