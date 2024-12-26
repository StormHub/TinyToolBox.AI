namespace TinyToolBox.AI.ChatCompletion.SemanticKernel;

public sealed class AzureCosmosDbConfig
{
    public required string Endpoint { get; init; }
    
    public string? APIKey { get; init; }    
}