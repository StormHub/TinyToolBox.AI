using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using TinyToolBox.AI.Evaluation.Templates;

namespace TinyToolBox.AI.Evaluation;

internal sealed partial class PromptTemplate
{
    private readonly CompositeFormat _format;
    private readonly ImmutableArray<PromptTemplateParameter> _parameters;
    private readonly ImmutableDictionary<string, float> _choiceScores;

    private PromptTemplate(
        string name, 
        CompositeFormat format,
        IReadOnlyCollection<PromptTemplateParameter> parameters, 
        IEnumerable<KeyValuePair<string, float>> choiceScores)
    {
        if (format.MinimumArgumentCount != parameters.Count)
        {
            throw new ArgumentException("Number of template format arguments must be the same as argument names");
        }

        Name = name;
        _format = format;
        _parameters = [..parameters];
        _choiceScores = choiceScores.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);
    }

    public string Name { get; }

    public IEnumerable<KeyValuePair<string, float>> GetChoiceScores() => _choiceScores.ToArray();

    public bool TryScore(string choice, [NotNullWhen(true)] out float? value)
    {
        value = default;
        if (_choiceScores.TryGetValue(choice, out var result))
        {
            value = result;
            return true;
        }

        return false;
    }

    internal IReadOnlyCollection<string> GetParameterNames() => 
        _parameters.Select(x => x.GetKey()).ToArray();

    public string Format(params KeyValuePair<string, object>[] args) => 
        string.Format(CultureInfo.CurrentCulture, _format, RequireParameters(args).ToArray());

    private IEnumerable<object> RequireParameters(KeyValuePair<string, object>[] args)
    {
        var map = new Dictionary<string, object>(args, StringComparer.OrdinalIgnoreCase);
        foreach (var argument in _parameters.OrderBy(x => x.Index))
        {
            var key = argument.GetKey();
            if (!map.TryGetValue(key, out var value))
            {
                throw new InvalidOperationException($"Parameter {key} required for template {Name}");
            }

            yield return value;
        }
    }
    
    public static IEnumerable<PromptTemplate> DefaultCollection()
    {
        var pattern = TokenPattern();
        foreach (var (name, yaml) in YamlTemplate.FromManifestResource())
        {
            var names = new HashSet<string>(
                pattern.Matches(yaml.Prompt)
                    .Select(x => x.Value)
                    .Where(x => !string.IsNullOrEmpty(x)));

            var formatString = yaml.Prompt;
            var parameterNames = names.ToArray();
            
            var parameters = new List<PromptTemplateParameter>();
            for (var i = 0; i < parameterNames.Length; i++)
            {
                formatString = formatString.Replace(parameterNames[i], $"{{{i}}}");
                parameters.Add(new PromptTemplateParameter(i, parameterNames[i]));
            }

            var format = CompositeFormat.Parse(formatString);
            yield return new PromptTemplate(
                name, 
                format, 
                parameters, 
                yaml.ChoiceScores);
        }
    }

    [GeneratedRegex(@"\{\{\{[a-zA-Z]+\}\}\}")]
    private static partial Regex TokenPattern();
}