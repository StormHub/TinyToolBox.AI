namespace TinyToolBox.AI.Evaluation.SemanticKernel.Properties;

public sealed class AzureOpenAIConfig
{
    public required string Endpoint { get; init; }

    public string? APIKey { get; init; }
    
    public required string ChatCompletionDeployment { get; init; }
    
    public required string TextEmbeddingGenerationModel { get; init; }
}