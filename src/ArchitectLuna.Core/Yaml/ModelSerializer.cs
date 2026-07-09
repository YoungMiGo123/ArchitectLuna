using ArchitectLuna.Core.Model;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ArchitectLuna.Core.Yaml;

public static class ModelSerializer
{
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public static string Serialize(ArchitectModel model) => Serializer.Serialize(model);

    public static ArchitectModel Deserialize(string yaml) => Deserializer.Deserialize<ArchitectModel>(yaml);

    public static ArchitectModel Load(string path) => Deserialize(File.ReadAllText(path));

    public static void Save(string path, ArchitectModel model) => File.WriteAllText(path, Serialize(model));
}
