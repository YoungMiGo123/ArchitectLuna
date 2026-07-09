namespace ArchitectLuna.Core.Model;

/// <summary>
/// The source of truth for a feature's domain data. Everything else — commands, queries,
/// handlers, validators, endpoints — is generated outward from an entity via
/// <see cref="CrudSynthesizer"/>, not authored independently of it.
/// </summary>
public sealed class EntityModel
{
    public required string Name { get; init; }

    public List<FieldModel> Fields { get; init; } = new();
}
