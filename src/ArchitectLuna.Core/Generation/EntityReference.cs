using ArchitectLuna.Core.Model;

namespace ArchitectLuna.Core.Generation;

/// <summary>An entity plus the feature that owns it, for persistence generators that need whole-model visibility (e.g. a DbContext needs one DbSet per entity across every feature).</summary>
public sealed record EntityReference(FeatureModel Feature, EntityModel Entity);
