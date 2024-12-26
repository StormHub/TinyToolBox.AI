namespace TinyToolBox.AI.Agents;

public sealed class CosmosChatHistoryStoreOptions
{
    private const int DefaultTimeToLive = 60 * 60; // one hour

    public required string DatabaseName { get; init; }

    public required string ContainerName { get; init; }

    public int DefaultTimeToLiveInSeconds { get; init; } = DefaultTimeToLive;
}