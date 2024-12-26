namespace TinyToolBox.AI.ChatCompletion.SemanticKernel;

public sealed class AzureOpenAIConfig
{
    public required string Endpoint { get; init; }

    public string? APIKey { get; init; }
    
    public required string ChatCompletionDeployment { get; init; }
}