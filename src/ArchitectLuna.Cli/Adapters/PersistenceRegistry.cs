using ArchitectLuna.Core.Generation;
using ArchitectLuna.Core.Model;
using ArchitectLuna.Persistence.EfCore;

namespace ArchitectLuna.Cli.Adapters;

public static class PersistenceRegistry
{
    public static readonly string[] KnownProviders = { "none", "efcore-postgres", "efcore-sqlserver", "marten" };

    public static IPersistenceGenerator Resolve(string providerName) => providerName switch
    {
        "none" => new NullPersistenceGenerator(),
        "efcore-postgres" => new EfCorePersistenceGenerator(EfCoreProviderKind.Postgres),
        "efcore-sqlserver" => new EfCorePersistenceGenerator(EfCoreProviderKind.SqlServer),
        "marten" => throw new InvalidOperationException("The 'marten' persistence provider is not implemented yet."),
        _ => throw new InvalidOperationException(
            $"Unknown --persistence value '{providerName}'. Valid values: {string.Join(", ", KnownProviders)}."),
    };

    public static IPersistenceGenerator Resolve(PersistenceProvider provider) => provider switch
    {
        PersistenceProvider.None => new NullPersistenceGenerator(),
        PersistenceProvider.EfCorePostgres => new EfCorePersistenceGenerator(EfCoreProviderKind.Postgres),
        PersistenceProvider.EfCoreSqlServer => new EfCorePersistenceGenerator(EfCoreProviderKind.SqlServer),
        _ => throw new InvalidOperationException($"Unknown persistence provider '{provider}'."),
    };

    public static PersistenceProvider ParseProvider(string providerName) => providerName switch
    {
        "none" => PersistenceProvider.None,
        "efcore-postgres" => PersistenceProvider.EfCorePostgres,
        "efcore-sqlserver" => PersistenceProvider.EfCoreSqlServer,
        "marten" => PersistenceProvider.Marten,
        _ => throw new InvalidOperationException(
            $"Unknown --persistence value '{providerName}'. Valid values: {string.Join(", ", KnownProviders)}."),
    };
}
