namespace ArchitectLuna.Core.Model;

/// <summary>
/// When generated database changes (EF Core migrations, Marten schema objects) get applied.
/// See docs/requirements/003-improvements.md §9.
/// </summary>
public enum DatabaseApplyMode
{
    /// <summary>
    /// ArchitectLuna only generates the database configuration; developers apply migrations/
    /// schema changes by hand. The default — safest for any environment, including production.
    /// </summary>
    Manual,

    /// <summary>`architect-luna generate` applies database changes as part of the run. Useful for local development.</summary>
    OnGenerate,

    /// <summary>The generated API applies database changes at startup. Useful for internal/controlled environments only.</summary>
    OnStartup,
}

/// <summary>Database-related settings persisted on <see cref="ArchitectModel"/>.</summary>
public sealed class DatabaseSettings
{
    // Mutable (not init-only): `config set database.applyMode` changes this on an
    // already-deserialized model in place, unlike every other model edit which appends to a list.
    public DatabaseApplyMode ApplyMode { get; set; } = DatabaseApplyMode.Manual;
}
