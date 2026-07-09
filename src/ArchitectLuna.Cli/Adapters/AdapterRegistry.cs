using ArchitectLuna.Core.Generation;

namespace ArchitectLuna.Cli.Adapters;

public static class AdapterRegistry
{
    public static readonly string[] KnownAdapters = { "mediatr", "wolverine" };

    public static IFrameworkAdapter Resolve(string adapterName, IPersistenceGenerator? persistence = null) => adapterName switch
    {
        "mediatr" => new ArchitectLuna.Adapters.MediatR.MediatRAdapter(persistence),
        "wolverine" => new ArchitectLuna.Adapters.Wolverine.WolverineAdapter(persistence),
        _ => throw new InvalidOperationException(
            $"Unknown adapter '{adapterName}'. Valid values: {string.Join(", ", KnownAdapters)}."),
    };
}
