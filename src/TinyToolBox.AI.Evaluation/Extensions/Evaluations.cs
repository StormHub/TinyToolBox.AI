using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;
using TinyToolBox.AI.Evaluation.Templates;

namespace TinyToolBox.AI.Evaluation.Extensions;

public static partial class Evaluations
{
    public static async IAsyncEnumerable<KeyValuePair<string, (string, float)?>> Run(this Kernel kernel, 
        string json,
        JsonSerializerOptions? options = null,
        PromptExecutionSettings? executionSettings = default,
        IPromptTemplateFactory? promptTemplateFactory = default, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var map = JsonSerializer.Deserialize<Dictionary<string, KernelArguments>>(json, options)
            ?? throw new ArgumentException($"Invalid json string {json}", nameof(json));
        
        var configurations = PromptTemplateConfigurations()
            .ToDictionary(k => k.Name!, v => v);
        
        var settings = new Dictionary<string, PromptExecutionSettings>(StringComparer.OrdinalIgnoreCase);
        if (executionSettings is not null)
        {
            settings.Add(executionSettings.ServiceId ?? PromptExecutionSettings.DefaultServiceId, executionSettings);
        }
        
        foreach (var name in map.Keys)
        {
            if (configurations.TryGetValue(name, out var promptTemplateConfig))
            {
                var arguments = new KernelArguments(map[name], settings);
                var function = kernel.CreateFunctionFromPrompt(promptTemplateConfig, promptTemplateFactory);
                
                var functionResult = await kernel.InvokeAsync(function, arguments, cancellationToken);
                var result = functionResult.ScoreResult();
                yield return new KeyValuePair<string, (string, float)?>(name, result);
            }
        }
    }
    
    public static async Task<(string, float)?> Invoke(this Kernel kernel, 
        string name, 
        KernelArguments? arguments = default, 
        IPromptTemplateFactory? promptTemplateFactory = default,
        CancellationToken cancellationToken = default)
    {
        var function = kernel.EvaluationFunction(name, promptTemplateFactory);
        var functionResult = await kernel.InvokeAsync(function, arguments, cancellationToken);
        return functionResult.ScoreResult();
    }
    
    public static IEnumerable<KernelFunction> EvaluationFunctions(this Kernel kernel, IPromptTemplateFactory? promptTemplateFactory = null)
    {
        foreach (var promptTemplateConfig in PromptTemplateConfigurations())
        {
            yield return kernel.CreateFunctionFromPrompt(promptTemplateConfig, promptTemplateFactory);
        }
    }

    public static KernelFunction EvaluationFunction(
        this Kernel kernel,
        string name,
        IPromptTemplateFactory? promptTemplateFactory = null)
    {
        var promptTemplateConfig = PromptTemplateConfigurations()
            .FirstOrDefault(x =>
                string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Template {name} does not exist", nameof(name));
        
        return kernel.CreateFunctionFromPrompt(promptTemplateConfig, promptTemplateFactory);
    }
    
    public static (string, float)? ScoreResult(this FunctionResult functionResult)
    {
        var value = functionResult.GetValue<string>();
        if (string.IsNullOrEmpty(value))
        {
            return default;
        }

        var executionSettings = functionResult.Function.ExecutionSettings;
        if (executionSettings is null)
        {
            return default;
        }

        if (!executionSettings.TryGetValue(PromptExecutionSettings.DefaultServiceId, out var settings) 
            || settings.ExtensionData is null)
        {
            return default;
        }

        if (!settings.ExtensionData.TryGetValue(nameof(YamlTemplate.ChoiceScores), out var data) 
            || data is not IReadOnlyDictionary<string, float> choiceScore)
        {
            return default;
        }

        return choiceScore.TryGetValue(value, out var scoreValue)
            ? (value, scoreValue)
            : default;
    }

    public static IEnumerable<PromptTemplateConfig> PromptTemplateConfigurations()
    {
        foreach (var (name, yaml) in YamlTemplate.FromManifestResource())
        {
            var promptTemplateConfig = yaml.ToPromptTemplateConfig(name);
            
            var executionSettings = new PromptExecutionSettings
            {
                ExtensionData = new Dictionary<string, object>
                {
                    { nameof(YamlTemplate.ChoiceScores), yaml.ChoiceScores }
                }
            };
            promptTemplateConfig.AddExecutionSettings(executionSettings);
            promptTemplateConfig.OutputVariable = new OutputVariable
            {
                Description = nameof(YamlTemplate.ChoiceScores) 
            };

            yield return promptTemplateConfig;
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

        var promptTemplateConfig = new PromptTemplateConfig(template)
        {
            Name = name
        };
        
        return promptTemplateConfig;
    }

    [GeneratedRegex(@"\{\{\{[a-zA-Z]+\}\}\}")]
    private static partial Regex TokenPattern();
}