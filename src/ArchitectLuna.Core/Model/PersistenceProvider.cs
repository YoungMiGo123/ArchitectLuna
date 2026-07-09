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
    EfCorePostgres,
    EfCoreSqlServer,
    Marten,
}
