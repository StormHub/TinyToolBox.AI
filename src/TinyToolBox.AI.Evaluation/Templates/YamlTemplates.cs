using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TinyToolBox.AI.Evaluation.Templates;

internal sealed class YamlTemplate
{
    public required string Prompt { get; init; }
    
    public required Dictionary<string, float> ChoiceScores { get; init; }
    
    public static IEnumerable<(string, YamlTemplate)> FromManifestResource()
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        foreach (var (name, stream) in GetYamlResources())
        {
            using var reader = new StreamReader(stream);
            var yamlPrompt = deserializer.Deserialize<YamlTemplate>(reader);
            
            yield return (name, yamlPrompt);
        }
    }

    private static IEnumerable<(string, Stream)> GetYamlResources()
    {
        var assembly = typeof(YamlTemplate).Assembly;
        var prefix = $"{typeof(YamlTemplate).Namespace}.";
        
        foreach (var resourceName in assembly.GetManifestResourceNames()
                     .Where(x => x.StartsWith(prefix)))
        {
            var extension = Path.GetExtension(resourceName);
            if (string.Equals(extension, ".yaml", StringComparison.OrdinalIgnoreCase))
            {
                var resourceStream = assembly.GetManifestResourceStream(resourceName)
                    ?? throw new FileNotFoundException($"{resourceName} resource not found");

                var name = Path.GetFileNameWithoutExtension(resourceName[prefix.Length..]);
                yield return (name, resourceStream);
            }
        }
    }
}
