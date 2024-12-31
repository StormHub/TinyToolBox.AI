namespace TinyToolBox.AI.Agents.SemanticKernel.Cosmos;

public sealed class AzureCosmosOptions
{
    public required string Endpoint { get; init; }
    
    public string? APIKey { get; init; }    
}