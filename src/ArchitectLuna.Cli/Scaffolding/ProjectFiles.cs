using System.Text;

namespace ArchitectLuna.Cli.Scaffolding;

/// <summary>
/// Builds .csproj and Directory.Build.props content. TargetFramework/ImplicitUsings/Nullable live
/// once in Directory.Build.props so every generated .csproj stays a couple of lines — matching the
/// "compiles immediately, no ceremony" requirement — instead of repeating the same PropertyGroup in
/// every project file.
/// </summary>
public static class ProjectFiles
{
    public const string DirectoryBuildProps =
        """
        <Project>

          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
          </PropertyGroup>

        </Project>
        """;

    public static string WebProject(IEnumerable<string>? projectReferences = null) =>
        Build("Microsoft.NET.Sdk.Web", projectReferences: projectReferences);

    public static string ClassLibrary(IEnumerable<string>? projectReferences = null) =>
        Build("Microsoft.NET.Sdk", projectReferences: projectReferences);

    public static string TestProject(IEnumerable<string>? projectReferences = null) =>
        Build("Microsoft.NET.Sdk", extraProperties: new[] { "<IsPackable>false</IsPackable>" }, projectReferences: projectReferences);

    private static string Build(string sdk, IReadOnlyList<string>? extraProperties = null, IEnumerable<string>? projectReferences = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<Project Sdk=\"{sdk}\">");

        if (extraProperties is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("  <PropertyGroup>");
            foreach (var property in extraProperties)
            {
                sb.AppendLine($"    {property}");
            }

            sb.AppendLine("  </PropertyGroup>");
        }

        var refs = projectReferences?.ToList() ?? new List<string>();
        if (refs.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("  <ItemGroup>");
            foreach (var reference in refs)
            {
                sb.AppendLine($"    <ProjectReference Include=\"{reference}\" />");
            }

            sb.AppendLine("  </ItemGroup>");
        }

        sb.AppendLine();
        sb.Append("</Project>");
        return sb.ToString();
    }
}
