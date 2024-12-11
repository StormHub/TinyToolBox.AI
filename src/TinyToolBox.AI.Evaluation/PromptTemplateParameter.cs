namespace TinyToolBox.AI.Evaluation;

internal readonly struct PromptTemplateParameter(int index, string name)
{
    public int Index { get; } = index;

    public string Name { get; } = name;

    public string GetKey() => Name.Trim('{', '}');
}