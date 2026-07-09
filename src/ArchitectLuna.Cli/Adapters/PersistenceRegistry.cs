using ArchitectLuna.Core.Generation;
using ArchitectLuna.Core.Model;
using ArchitectLuna.Persistence.EfCore;
using ArchitectLuna.Persistence.InMemory;
using ArchitectLuna.Persistence.Marten;

namespace ArchitectLuna.Cli.Adapters;

public static class PersistenceRegistry
{
    public static readonly string[] KnownProviders = { "none", "in-memory", "efcore-postgres", "efcore-sqlserver", "marten" };

    public static IPersistenceGenerator Resolve(string providerName) => providerName switch
    {
        "none" => new NullPersistenceGenerator(),
        "in-memory" => new InMemoryPersistenceGenerator(),
        "efcore-postgres" => new EfCorePersistenceGenerator(EfCoreProviderKind.Postgres),
        "efcore-sqlserver" => new EfCorePersistenceGenerator(EfCoreProviderKind.SqlServer),
        "marten" => new MartenPersistenceGenerator(),
        _ => throw new InvalidOperationException(
            $"Unknown --persistence value '{providerName}'. Valid values: {string.Join(", ", KnownProviders)}."),
    };

    public static IPersistenceGenerator Resolve(PersistenceProvider provider) => provider switch
    {
        PersistenceProvider.None => new NullPersistenceGenerator(),
        PersistenceProvider.InMemory => new InMemoryPersistenceGenerator(),
        PersistenceProvider.EfCorePostgres => new EfCorePersistenceGenerator(EfCoreProviderKind.Postgres),
        PersistenceProvider.EfCoreSqlServer => new EfCorePersistenceGenerator(EfCoreProviderKind.SqlServer),
        PersistenceProvider.Marten => new MartenPersistenceGenerator(),
        _ => throw new InvalidOperationException($"Unknown persistence provider '{provider}'."),
    };

    public static PersistenceProvider ParseProvider(string providerName) => providerName switch
    {
        "none" => PersistenceProvider.None,
        "in-memory" => PersistenceProvider.InMemory,
        "efcore-postgres" => PersistenceProvider.EfCorePostgres,
        "efcore-sqlserver" => PersistenceProvider.EfCoreSqlServer,
        "marten" => PersistenceProvider.Marten,
        _ => throw new InvalidOperationException(
            $"Unknown --persistence value '{providerName}'. Valid values: {string.Join(", ", KnownProviders)}."),
    };
}
