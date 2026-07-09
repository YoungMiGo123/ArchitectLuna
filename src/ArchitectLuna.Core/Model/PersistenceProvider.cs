namespace ArchitectLuna.Core.Model;

/// <summary>
/// Which persistence backend (if any) generated CRUD handlers should use. Orthogonal to
/// <see cref="ArchitectModel.Adapter"/>: any messaging adapter can pair with any provider, though
/// Marten is the most idiomatic pairing with Wolverine (both are part of the same "critter stack"
/// ecosystem).
/// </summary>
public enum PersistenceProvider
{
    /// <summary>No persistence wired up — handler bodies stay `throw new NotImplementedException()`.</summary>
    None,

    /// <summary>
    /// Real CRUD backed by a process-lifetime, thread-safe in-memory store — no database, no
    /// extra NuGet packages, no connection string. Not durable across restarts; a deliberate
    /// tradeoff so a freshly scaffolded API has working handlers with zero external setup. The
    /// default for `new api`.
    /// </summary>
    InMemory,
    EfCorePostgres,
    EfCoreSqlServer,
    Marten,
}
