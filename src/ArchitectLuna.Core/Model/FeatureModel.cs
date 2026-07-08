namespace ArchitectLuna.Core.Model;

public sealed class FeatureModel
{
    public required string Name { get; init; }

    public List<EntityModel> Entities { get; init; } = new();

    public List<CommandModel> Commands { get; init; } = new();

    public List<QueryModel> Queries { get; init; } = new();
}
