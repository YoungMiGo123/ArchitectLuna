using System.Reflection;

namespace ArchitectLuna.Templates;

/// <summary>
/// Reads .sbn template text from this assembly's embedded resources
/// (Templates/{adapterName}/{templateFileName}).
/// </summary>
public sealed class EmbeddedTemplateProvider
{
    private readonly Assembly _assembly = typeof(EmbeddedTemplateProvider).Assembly;

    public string GetTemplate(string adapterName, string templateFileName)
    {
        var suffix = $".{adapterName}.{templateFileName}";
        var resourceName = _assembly.GetManifestResourceNames()
            .SingleOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            var available = string.Join(", ", _assembly.GetManifestResourceNames());
            throw new FileNotFoundException(
                $"No embedded template resource ending with '{suffix}' was found. Available resources: {available}");
        }

        using var stream = _assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
