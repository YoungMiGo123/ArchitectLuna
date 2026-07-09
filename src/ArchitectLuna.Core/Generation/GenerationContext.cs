namespace ArchitectLuna.Core.Generation;

/// <summary>
/// Everything an adapter/persistence generator needs to place and namespace generated files,
/// independent of the on-disk layout decisions made by the CLI's scaffolder. Four independent
/// project targets so vertical-slice (one physical project, everything collapses together) and
/// Clean Architecture (four real projects) are both expressible without either adapters or
/// persistence generators needing to know which layout is in play — they always ask for "the
/// Application target" or "the Domain target" and get the right answer.
/// </summary>
public sealed record GenerationContext(
    string RootNamespace,
    ProjectTarget Api,
    ProjectTarget Application,
    ProjectTarget Domain,
    ProjectTarget Infrastructure)
{
    /// <summary>
    /// One project, one namespace — today's shape. Domain/Infrastructure both resolve to a
    /// "Persistence" sub-namespace/folder under the Api project, matching the paths and
    /// namespaces persistence generators have always used, so this is a byte-for-byte-compatible
    /// factory, not just a behaviorally-similar one.
    /// </summary>
    public static GenerationContext ForVerticalSlice(string rootNamespace, string apiProjectRoot)
    {
        var apiTarget = new ProjectTarget(apiProjectRoot, rootNamespace);
        var persistenceTarget = new ProjectTarget($"{apiProjectRoot}/Persistence", $"{rootNamespace}.Persistence");
        return new GenerationContext(rootNamespace, apiTarget, apiTarget, persistenceTarget, persistenceTarget);
    }

    /// <summary>Four real projects, dependency rule pointing inward (Api/Infrastructure → Application → Domain).</summary>
    public static GenerationContext ForCleanArchitecture(
        string rootNamespace,
        string apiProjectRoot,
        string applicationProjectRoot,
        string domainProjectRoot,
        string infrastructureProjectRoot) =>
        new(
            rootNamespace,
            new ProjectTarget(apiProjectRoot, $"{rootNamespace}.Api"),
            new ProjectTarget(applicationProjectRoot, $"{rootNamespace}.Application"),
            new ProjectTarget(domainProjectRoot, $"{rootNamespace}.Domain"),
            new ProjectTarget(infrastructureProjectRoot, $"{rootNamespace}.Infrastructure"));

    /// <summary>
    /// True when Domain and Infrastructure are genuinely separate projects (Clean Architecture) —
    /// the signal persistence generators use to depend on an abstraction they own (an interface)
    /// rather than a concrete type from another project, so Application never needs a project
    /// reference to Infrastructure. False for vertical slice, where there's only one project
    /// anyway and the extra indirection would be pure ceremony.
    /// </summary>
    public bool HasSeparateInfrastructure => Domain.ProjectRoot != Infrastructure.ProjectRoot;
}
