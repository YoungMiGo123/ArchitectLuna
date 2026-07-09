namespace ArchitectLuna.Core.Model;

/// <summary>
/// Output shape for `new api`. Both layouts share the same Intent Model, adapters, and
/// persistence providers — this only changes which project a generated file lands in and how
/// many projects the solution has, never the generated code's logic.
/// </summary>
public enum SolutionLayout
{
    /// <summary>One Api project with a Features/ folder per command/query — today's shape.</summary>
    VerticalSlice,

    /// <summary>Api/Application/Domain/Infrastructure projects, dependency rule pointing inward.</summary>
    CleanArchitecture,
}
