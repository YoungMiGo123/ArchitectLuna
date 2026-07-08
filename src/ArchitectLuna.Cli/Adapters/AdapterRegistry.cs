using ArchitectLuna.Core.Generation;

namespace ArchitectLuna.Cli.Adapters;

public static class AdapterRegistry
{
    public static readonly string[] KnownAdapters = { "mediatr", "wolverine" };

    public static IFrameworkAdapter Resolve(string adapterName) => adapterName switch
    {
        "mediatr" => new ArchitectLuna.Adapters.MediatR.MediatRAdapter(),
        "wolverine" => new ArchitectLuna.Adapters.Wolverine.WolverineAdapter(),
        _ => throw new InvalidOperationException(
            $"Unknown adapter '{adapterName}'. Valid values: {string.Join(", ", KnownAdapters)}."),
    };
}
