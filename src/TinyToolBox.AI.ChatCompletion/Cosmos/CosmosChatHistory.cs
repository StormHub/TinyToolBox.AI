using Microsoft.Extensions.AI;

namespace TinyToolBox.AI.ChatCompletion.Cosmos;

internal record CosmosChatHistoryKey(string Id, string ThreadId);

internal sealed class CosmosChatHistory
{
    internal const string PartitionKeyPath = "/threadId";

    public required string Id { get; init; }

    public required string ThreadId { get; init; }

    public required List<ChatMessage> Messages { get; init; } = [];
}