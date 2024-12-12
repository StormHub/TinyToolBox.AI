using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;
using TinyToolBox.AI.Evaluation.Templates;

namespace TinyToolBox.AI.Evaluation.Extensions;

public static partial class Templates
{
    public static IEnumerable<PromptTemplateConfig> DefaultConfigurations()
    {
        foreach (var (name, yaml) in YamlTemplate.FromManifestResource())
        {
            yield return yaml.ToPromptTemplateConfig(name);
        }
    }
    
    internal static PromptTemplateConfig ToPromptTemplateConfig(this YamlTemplate yamlTemplate, string name)
    {
        var pattern = TokenPattern();
        
        var template = yamlTemplate.Prompt;
        while (pattern.IsMatch(template))
        {
            template = pattern.Replace(template,
                match =>
                {
                    var token = match.Groups[0].Value;
                    return $"{{{{${token.Trim('{', '}')}}}}}";
                });
        }
        
        template += $"\nReturn a string of choices, e.g. {string.Join(" or ", yamlTemplate.ChoiceScores.Keys)}"; 

        var config = new PromptTemplateConfig(template)
        {
            Name = name
        };

        return config;
    }

    [GeneratedRegex(@"\{\{\{[a-zA-Z]+\}\}\}")]
    private static partial Regex TokenPattern();
}